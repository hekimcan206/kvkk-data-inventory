using System.Text.RegularExpressions;
using KvkkInventory.Classification;
using KvkkInventory.Validation;

namespace KvkkInventory.Detection;

/// <summary>TCKN: 11 digits with two check digits. A random 11-digit number passes ≈ 1 % of the time.</summary>
public sealed partial class NationalIdRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("tckn", 0.95), ("tc kimlik", 0.95), ("tc kimlik no", 0.95), ("tcno", 0.9), ("tc no", 0.9), ("tc", 0.8),
        ("kimlik no", 0.9), ("kimlik numarasi", 0.9), ("ykn", 0.85), ("national id", 0.9), ("identity number", 0.9),
        ("citizen id", 0.85), ("citizenship number", 0.85));

    public override PiiType Type => PiiType.NationalId;

    public override double ValueStrength => 0.97;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value)
    {
        var trimmed = value.Trim();
        if (Checksums.IsValidTckn(trimmed))
        {
            return ValueMatch.Full;
        }

        foreach (Match match in Embedded().Matches(value))
        {
            if (Checksums.IsValidTckn(match.Value))
            {
                return ValueMatch.Embedded;
            }
        }

        return ValueMatch.None;
    }

    [GeneratedRegex(@"(?<!\d)[1-9]\d{10}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Embedded();
}

/// <summary>VKN: 10 digits with one check digit (≈ 10 % of random 10-digit numbers pass, hence the lower strength).</summary>
public sealed class TaxIdRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("vkn", 0.95), ("vergi no", 0.9), ("vergi kimlik", 0.95), ("vergi numarasi", 0.9), ("tax id", 0.9),
        ("tax number", 0.9), ("tin", 0.7));

    public override PiiType Type => PiiType.TaxId;

    public override double ValueStrength => 0.85;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value) =>
        Checksums.IsValidVkn(value.Trim()) ? ValueMatch.Full : ValueMatch.None;
}

/// <summary>IBAN: ISO 13616 mod-97 (TR: 26 characters). Accepts the usual grouped spelling "TR12 0006 …".</summary>
public sealed partial class IbanRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("iban", 0.95), ("hesap no", 0.7), ("hesap numarasi", 0.7), ("banka hesap", 0.7), ("account number", 0.7),
        ("bank account", 0.75));

    public override PiiType Type => PiiType.Iban;

    public override double ValueStrength => 0.97;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value)
    {
        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        if (Checksums.IsValidIban(compact))
        {
            return ValueMatch.Full;
        }

        foreach (Match match in Embedded().Matches(value.ToUpperInvariant()))
        {
            if (Checksums.IsValidIban(match.Value.Replace(" ", string.Empty, StringComparison.Ordinal)))
            {
                return ValueMatch.Embedded;
            }
        }

        return ValueMatch.None;
    }

    [GeneratedRegex(@"TR\d{2}(?: ?\d){22}", RegexOptions.CultureInvariant)]
    private static partial Regex Embedded();
}

/// <summary>Payment card number: Luhn-valid and a known issuer prefix (Visa, Mastercard, Amex, TROY 9792, …).</summary>
public sealed partial class PaymentCardRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("kart no", 0.9), ("kart numarasi", 0.9), ("kredi karti", 0.9), ("card number", 0.9), ("card no", 0.9),
        ("cc number", 0.85), ("pan", 0.7));

    public override PiiType Type => PiiType.PaymentCard;

    public override double ValueStrength => 0.95;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    internal static bool IsCardNumber(string digits) => Checksums.IsValidLuhn(digits) && HasKnownPrefix(digits);

    protected override ValueMatch MatchValue(string value)
    {
        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        if (IsCardNumber(compact))
        {
            return ValueMatch.Full;
        }

        foreach (Match match in Embedded().Matches(value))
        {
            var digits = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
            if (IsCardNumber(digits))
            {
                return ValueMatch.Embedded;
            }
        }

        return ValueMatch.None;
    }

    private static bool HasKnownPrefix(string digits)
    {
        if (digits.Length < 13)
        {
            return false;
        }

        var two = int.Parse(digits.AsSpan(0, 2), provider: System.Globalization.CultureInfo.InvariantCulture);
        var four = int.Parse(digits.AsSpan(0, 4), provider: System.Globalization.CultureInfo.InvariantCulture);
        return digits[0] == '4'                       // Visa
            || two is >= 51 and <= 55                 // Mastercard
            || four is >= 2221 and <= 2720            // Mastercard 2-series
            || two is 34 or 37                        // American Express
            || four == 9792                           // TROY (Türkiye)
            || two == 35                              // JCB
            || digits[0] == '6';                      // Discover / UnionPay
    }

    [GeneratedRegex(@"(?<!\d)\d(?:[ -]?\d){12,18}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Embedded();
}
