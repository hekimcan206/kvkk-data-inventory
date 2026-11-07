using System.Net;
using System.Text.RegularExpressions;
using KvkkInventory.Classification;
using KvkkInventory.Text;

namespace KvkkInventory.Detection;

public sealed partial class EmailRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("eposta", 0.95), ("e posta", 0.95), ("email", 0.95), ("e mail", 0.95), ("mail", 0.8),
        ("elektronik posta", 0.95), ("mail adresi", 0.95));

    public override PiiType Type => PiiType.Email;

    public override double ValueStrength => 0.95;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value) =>
        Full().IsMatch(value.Trim()) ? ValueMatch.Full
        : Embedded().IsMatch(value) ? ValueMatch.Embedded
        : ValueMatch.None;

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex Full();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Embedded();
}

/// <summary>
/// Turkish phone numbers: +90 / 0090 / 90 / 0 prefix followed by a 10-digit national number starting with
/// 2–5 (landline area codes 2xx–4xx, mobile 5xx) or 850. A bare 10-digit number counts only when it is mobile
/// (starts with 5); a bare 10-digit number starting with 2–4 is far more often a VKN or an internal id.
/// </summary>
public sealed partial class PhoneRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("telefon", 0.95), ("tel", 0.8), ("gsm", 0.9), ("cep", 0.8), ("cep tel", 0.95), ("cep telefonu", 0.95),
        ("phone", 0.95), ("mobile", 0.85), ("msisdn", 0.9), ("faks", 0.6), ("fax", 0.6), ("telephone", 0.95));

    public override PiiType Type => PiiType.Phone;

    public override double ValueStrength => 0.85;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    internal static bool IsPhone(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is < 10 or > 20 || !Shape().IsMatch(trimmed))
        {
            return false;
        }

        var digits = TurkishText.DigitsOnly(trimmed);
        var national = digits switch
        {
            { Length: 14 } when digits.StartsWith("0090", StringComparison.Ordinal) => digits[4..],
            { Length: 12 } when digits.StartsWith("90", StringComparison.Ordinal) => digits[2..],
            { Length: 11 } when digits[0] == '0' => digits[1..],
            { Length: 10 } when digits[0] == '5' => digits,
            _ => null,
        };

        return national is not null && (national[0] is >= '2' and <= '5' || national.StartsWith("850", StringComparison.Ordinal));
    }

    protected override ValueMatch MatchValue(string value) =>
        IsPhone(value) ? ValueMatch.Full
        : Embedded().IsMatch(value) ? ValueMatch.Embedded
        : ValueMatch.None;

    [GeneratedRegex(@"^\+?[\d\s().-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    [GeneratedRegex(@"(?<!\d)(?:\+90|0)\s?\(?5\d{2}\)?[\s.-]?\d{3}[\s.-]?\d{2}[\s.-]?\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Embedded();
}

/// <summary>
/// Turkish postal addresses: at least two address markers (Mah., Sok., Cad., No:, Apt., Daire …) or one marker
/// plus a province name.
/// </summary>
public sealed partial class AddressRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("adres", 0.95), ("address", 0.95), ("ikamet", 0.85), ("ikametgah", 0.9), ("sokak", 0.7), ("mahalle", 0.7),
        ("street", 0.7), ("acik adres", 0.95));

    private static readonly HashSet<string> NotAboutPeople = ["sube", "branch", "hastane", "hospital", "firma", "company", "kurum", "depo", "warehouse"];

    public override PiiType Type => PiiType.Address;

    public override double ValueStrength => 0.85;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override IReadOnlySet<string> NegativeTokens => NotAboutPeople;

    protected override ValueMatch MatchValue(string value)
    {
        if (value.Length < 10)
        {
            return ValueMatch.None;
        }

        var markers = new HashSet<string>(StringComparer.Ordinal);
        var province = false;
        foreach (var word in Words().Split(TurkishText.Fold(value)))
        {
            if (Lexicons.AddressMarkers.Contains(word))
            {
                markers.Add(word);
            }
            else if (Lexicons.Provinces.Contains(word))
            {
                province = true;
            }
        }

        return markers.Count >= 2 || (markers.Count == 1 && province) ? ValueMatch.Full : ValueMatch.None;
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();
}

public sealed partial class IpAddressRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("ip", 0.85), ("ip adresi", 0.95), ("ip address", 0.95), ("client ip", 0.9), ("remote addr", 0.85),
        ("ipv4", 0.9), ("ipv6", 0.9));

    public override PiiType Type => PiiType.IpAddress;

    public override double ValueStrength => 0.9;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value)
    {
        var trimmed = value.Trim();
        if (V4().IsMatch(trimmed) || (trimmed.Contains(':', StringComparison.Ordinal) && trimmed.Length >= 3
            && IPAddress.TryParse(trimmed, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6))
        {
            return ValueMatch.Full;
        }

        return V4Embedded().IsMatch(value) ? ValueMatch.Embedded : ValueMatch.None;
    }

    [GeneratedRegex(@"^(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$", RegexOptions.CultureInvariant)]
    private static partial Regex V4();

    [GeneratedRegex(@"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?![\d.])", RegexOptions.CultureInvariant)]
    private static partial Regex V4Embedded();
}

/// <summary>
/// Turkish vehicle plates: province code 01–81, then 1 letter + 4–5 digits, 2 letters + 3–4 digits or
/// 3 letters + 2–3 digits. Q, W and X are not used.
/// </summary>
public sealed partial class VehiclePlateRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(("plaka", 0.95), ("plate", 0.85), ("arac plaka", 0.95), ("license plate", 0.95));

    public override PiiType Type => PiiType.VehiclePlate;

    public override double ValueStrength => 0.85;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value)
    {
        var match = Plate().Match(value.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            return ValueMatch.None;
        }

        var letters = match.Groups["l"].Value.Length;
        var digits = match.Groups["d"].Value.Length;
        var valid = (letters, digits) switch
        {
            (1, 4 or 5) or (2, 3 or 4) or (3, 2 or 3) => true,
            _ => false,
        };
        return valid ? ValueMatch.Full : ValueMatch.None;
    }

    [GeneratedRegex(@"^(?:0[1-9]|[1-7]\d|8[01]) ?(?<l>[A-PR-VYZ]{1,3}) ?(?<d>\d{2,5})$", RegexOptions.CultureInvariant)]
    private static partial Regex Plate();
}
