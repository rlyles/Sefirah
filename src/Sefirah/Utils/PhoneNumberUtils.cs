using System.Globalization;
using System.Text.RegularExpressions;
using PhoneNumbers;

namespace Sefirah.Utils;

public static partial class PhoneNumberUtils
{
    private static readonly PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
    private static readonly Regex NonPhoneCharactersRegex = PhoneNumberRegex();

    // Default region for numbers stored without a country code (e.g. "(864) 555-1234").
    // RegionInfo.CurrentRegion follows the Windows "Country or region" setting, which can be
    // unset or something libphonenumber doesn't accept; fall back to the culture's region.
    // "ZZ" (unknown) still parses numbers that carry a +country code.
    private static readonly string currentRegionInfo = ResolveDefaultRegion();

    private static string ResolveDefaultRegion()
    {
        var supported = phoneNumberUtil.GetSupportedRegions();
        Func<string>[] candidates =
        [
            () => RegionInfo.CurrentRegion.TwoLetterISORegionName,
            () => new RegionInfo(CultureInfo.CurrentCulture.Name).TwoLetterISORegionName,
            () => new RegionInfo(CultureInfo.InstalledUICulture.Name).TwoLetterISORegionName,
        ];

        foreach (var candidate in candidates)
        {
            try
            {
                var region = candidate()?.ToUpperInvariant();
                if (region is not null && supported.Contains(region))
                    return region;
            }
            catch (ArgumentException)
            {
                // Neutral or invariant culture with no region; try the next source.
            }
        }

        return "ZZ";
    }

    public static bool IsSemanticMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftParsed = TryParse(left);
        var rightParsed = TryParse(right);

        PhoneNumberUtil.MatchType matchType;
        if (leftParsed is not null && rightParsed is not null)
        {
            matchType = phoneNumberUtil.IsNumberMatch(leftParsed, rightParsed);
        }
        else
        {
            // At least one side couldn't be parsed with the default region (typically a contact
            // saved without +1 when the PC's region isn't usable). The string overload borrows the
            // country code from whichever number has one, so "+18645551234" matches "(864) 555-1234".
            if (!IsPhoneNumber(left) || !IsPhoneNumber(right))
            {
                return false;
            }

            matchType = phoneNumberUtil.IsNumberMatch(left.Trim(), right.Trim());
        }

        return matchType is PhoneNumberUtil.MatchType.EXACT_MATCH or PhoneNumberUtil.MatchType.NSN_MATCH;
    }

    /// <summary>
    /// Whether two address strings refer to the same phone number (case-insensitive exact, then libphonenumber semantic).
    /// </summary>
    public static bool IsMatch(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
        || IsSemanticMatch(left, right);

    private static PhoneNumber? TryParse(string value)
    {
        var normalizedInput = value.Trim();
        if (!LooksLikePhoneNumber(normalizedInput))
        {
            return null;
        }

        try
        {
            return phoneNumberUtil.Parse(normalizedInput, currentRegionInfo);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    /// <summary>
    /// Google Messages files RCS group threads under a single placeholder recipient
    /// ("&lt;id&gt;@rcs.google.com"); the real participants only appear on each message.
    /// </summary>
    public static bool IsRcsGroupPlaceholder(string? value) =>
        value is not null && value.Trim().EndsWith("@rcs.google.com", StringComparison.OrdinalIgnoreCase);

    public static bool IsPhoneNumber(string? value) =>
        !string.IsNullOrWhiteSpace(value) && LooksLikePhoneNumber(value.Trim());

    /// <summary>
    /// Canonical form for storage and cache keys (E.164 when parseable, otherwise trimmed input).
    /// </summary>
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        var parsed = TryParse(trimmed);
        return parsed is not null
            ? phoneNumberUtil.Format(parsed, PhoneNumberFormat.E164)
            : trimmed;
    }

    private static bool LooksLikePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (NonPhoneCharactersRegex.IsMatch(value))
        {
            return false;
        }

        var digitCount = value.Count(char.IsDigit);
        return digitCount is >= 3 and <= 20;
    }

    [GeneratedRegex(@"[^\d\+\-\(\)\.\s]", RegexOptions.Compiled)]
    private static partial Regex PhoneNumberRegex();
}
