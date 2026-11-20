using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Utils;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ConversationEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public long ThreadId { get; set; }

    public string? AddressesJson { get; set; }

    public long LastMessageTimestamp { get; set; }

    public string? LastMessage { get; set; }

    public bool HasRead { get; set; }

    public long TimeStamp { get; set; }

    public long Checksum { get; set; }

    // Display-only roster for RCS group threads, gathered from message addresses.
    // AddressesJson stays the thread's real recipient list (used for sending).
    public string? ParticipantsJson { get; set; }

    [Ignore]
    public List<string> Addresses { get; set; } = [];

    #region Helpers
    public static string GetKey(string deviceId, long threadId) => $"{deviceId}:{threadId}";

    private bool IsRcsGroupThread()
    {
        var recipients = string.IsNullOrEmpty(AddressesJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(AddressesJson) ?? [];
        return recipients.Count > 0 && recipients.All(PhoneNumberUtils.IsRcsGroupPlaceholder);
    }

    /// <summary>
    /// For RCS group threads, adds any new participants found on these messages to the display roster.
    /// Returns true if the roster changed.
    /// </summary>
    internal bool MergeRcsParticipants(IEnumerable<TextMessage> messages)
    {
        if (!IsRcsGroupThread())
            return false;

        var participants = string.IsNullOrEmpty(ParticipantsJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(ParticipantsJson) ?? [];

        var changed = false;
        foreach (var address in messages.SelectMany(m => m.Addresses))
        {
            if (string.IsNullOrWhiteSpace(address) || PhoneNumberUtils.IsRcsGroupPlaceholder(address))
                continue;
            if (participants.Any(p => PhoneNumberUtils.IsMatch(p, address)))
                continue;
            participants.Add(address.Trim());
            changed = true;
        }

        if (changed)
            ParticipantsJson = JsonSerializer.Serialize(participants);
        return changed;
    }

    internal Conversation ToConversation(ContactRepository contactRepository)
    {
        if (!string.IsNullOrEmpty(AddressesJson))
            Addresses = JsonSerializer.Deserialize<List<string>>(AddressesJson) ?? [];

        var contacts = new List<Contact>();
        foreach (var address in Addresses)
        {
            var contact = contactRepository.GetContact(DeviceId, address);
            if (!contacts.Contains(contact))
                contacts.Add(contact);
        }

        var participants = new List<Contact>();
        if (!string.IsNullOrEmpty(ParticipantsJson) && IsRcsGroupThread())
        {
            foreach (var address in JsonSerializer.Deserialize<List<string>>(ParticipantsJson) ?? [])
            {
                var contact = contactRepository.GetContact(DeviceId, address);
                if (!participants.Contains(contact))
                    participants.Add(contact);
            }
        }

        return new Conversation
        {
            ConversationKey = Key,
            ThreadId = ThreadId,
            Contacts = contacts,
            Participants = participants,
            LastMessage = LastMessage ?? string.Empty,
            LastMessageTimestamp = LastMessageTimestamp,
            HasRead = HasRead,
            Checksum = Checksum,
        };
    }
    #endregion
}
