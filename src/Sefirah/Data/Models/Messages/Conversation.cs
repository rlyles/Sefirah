using System.Globalization;
using Sefirah.Utils;
using Windows.Storage.Streams;

namespace Sefirah.Data.Models.Messages;

public partial class Conversation : ObservableObject
{
    public string ConversationKey { get; set; } = string.Empty;

    public long ThreadId { get; set; }

    public List<Contact> Contacts { get; set; } = [];

    // RCS group threads: Contacts holds only the "...@rcs.google.com" placeholder (kept for sending);
    // Participants holds the people seen on its messages, for display.
    public List<Contact> Participants { get; set; } = [];

    public bool IsRcsGroup => Contacts.Count > 0 && Contacts.All(c => PhoneNumberUtils.IsRcsGroupPlaceholder(c.Address));

    // Group presentation follows the conversation's resolved participants.
    public bool IsGroup => Contacts.Count > 1 || IsRcsGroup;

    public Contact? PrimaryContact => Contacts.Count == 1 && !IsRcsGroup ? Contacts[0] : null;

    public string DisplayName => Contacts.Count == 0
        ? "Unknown"
        : IsRcsGroup
            ? Participants.Count > 0 ? string.Join(", ", Participants.Select(c => c.DisplayName)) : "Group chat"
            : string.Join(", ", Contacts.Select(c => c.DisplayName));

    public string SubtitleAddress => PrimaryContact?.Address ?? string.Empty;

    public IRandomAccessStreamReference? AvatarStream => PrimaryContact?.AvatarStream;

    public bool HasAvatarImage => PrimaryContact?.HasAvatar ?? false;

    public string PlaceholderColorHex => IsGroup
        ? ContactHelper.GetPlaceholderColorHex(ThreadId.ToString(CultureInfo.InvariantCulture))
        : PrimaryContact?.PlaceholderColorHex ?? ContactHelper.GetPlaceholderColorHex(ThreadId.ToString(CultureInfo.InvariantCulture));

    public string Initials => IsGroup ? string.Empty : PrimaryContact?.Initials ?? string.Empty;

    public bool ShowGroupGlyph => IsGroup;

    public bool ShowContactInitials => !IsGroup && !string.IsNullOrEmpty(Initials);

    public bool ShowContactGlyph => !IsGroup && string.IsNullOrEmpty(Initials);

    private string lastMessage = string.Empty;
    public string LastMessage
    {
        get => lastMessage;
        set => SetProperty(ref lastMessage, value);
    }

    private long lastMessageTimestamp;
    public long LastMessageTimestamp
    {
        get => lastMessageTimestamp;
        set => SetProperty(ref lastMessageTimestamp, value);
    }

    private bool hasRead;
    public bool HasRead
    {
        get => hasRead;
        set => SetProperty(ref hasRead, value);
    }

    public long Checksum { get; set; }

    // Phone inventory for this session. Null means unknown; empty means no messages.
    public ThreadMessageIndex? MessageIndex { get; set; }

    public bool IsTemporaryThread => ThreadId < 0;

    public void UpdateFrom(Conversation other)
    {
        LastMessage = other.LastMessage;
        LastMessageTimestamp = other.LastMessageTimestamp;
        HasRead = other.HasRead;
        Checksum = other.Checksum;
        Contacts = other.Contacts;
        Participants = other.Participants;
        if (other.MessageIndex is not null)
            MessageIndex = other.MessageIndex.Clone();
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(SubtitleAddress));
        OnPropertyChanged(nameof(AvatarStream));
        OnPropertyChanged(nameof(HasAvatarImage));
        OnPropertyChanged(nameof(IsGroup));
        OnPropertyChanged(nameof(PlaceholderColorHex));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(ShowGroupGlyph));
        OnPropertyChanged(nameof(ShowContactInitials));
        OnPropertyChanged(nameof(ShowContactGlyph));
    }
}
