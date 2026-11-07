using System.Globalization;
using System.Text;
using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Text;
using KvkkInventory.Validation;

namespace KvkkInventory.Masking;

/// <summary>
/// Masks single values. Every keyed strategy is a one-way function of (secret key, PII type, normalised value):
/// <list type="bullet">
/// <item>deterministic — the same TCKN masks to the same pseudonym in every table and every run, so foreign keys
/// and joins on masked columns keep working;</item>
/// <item>not reversible without the key — and, unlike format-preserving <i>encryption</i> (NIST FF1/FF3-1), not
/// decryptable even with it; a key holder can only confirm a guess (see docs/threat-model.md);</item>
/// <item>normalised first — <c>0532 111 22 33</c> and <c>+905321112233</c> are the same person and mask alike.</item>
/// </list>
/// </summary>
public sealed class Masker
{
    public const int MinimumKeyBytes = 32;

    private const string TokenAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string PlateLetters = "ABCDEFGHIJKLMNOPRSTUVYZ";
    private const string LowerLetters = "abcdefghijklmnopqrstuvwxyz";
    private const string UpperLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static readonly string[] FirstNames = [.. Lexicons.FirstNames.Order(StringComparer.Ordinal)];
    private static readonly string[] Surnames = [.. Lexicons.Surnames.Order(StringComparer.Ordinal)];
    private static readonly string[] Provinces = [.. Lexicons.Provinces.Order(StringComparer.Ordinal)];
    private static readonly string[] Streets = ["Atatürk", "Cumhuriyet", "İnönü", "Gazi", "Fatih", "Yıldız", "Lale", "Çınar", "Menekşe", "Barış", "Zafer", "Kardelen"];

    private readonly byte[] _key;

    public Masker(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < MinimumKeyBytes)
        {
            throw new ArgumentException($"The masking key must be at least {MinimumKeyBytes} bytes (256 bits).", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    /// <summary>Reads a base64 key, e.g. from the <c>KVKK_MASKING_KEY</c> environment variable.</summary>
    public static Masker FromBase64(string base64Key) => new(Convert.FromBase64String(base64Key));

    /// <summary>Masks a database value and converts the result back to the column's CLR type where possible.</summary>
    public object? Mask(object? value, PiiType type, MaskingStrategy strategy, int? maxLength = null)
    {
        if (value is null or DBNull || strategy == MaskingStrategy.Keep)
        {
            return value is DBNull ? null : value;
        }

        if (strategy == MaskingStrategy.Null)
        {
            return null;
        }

        if (value is DateTime date && type == PiiType.DateOfBirth)
        {
            return strategy == MaskingStrategy.Generalize ? new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind) : ShiftDate(date);
        }

        if (value is byte[])
        {
            return null;
        }

        var text = Schema.ValueFormatter.ToText(value) ?? string.Empty;
        var masked = MaskText(text, type, strategy);
        if (maxLength is > 0 && masked.Length > maxLength)
        {
            masked = masked[..maxLength.Value];
        }

        return ConvertBack(masked, value);
    }

    public string MaskText(string value, PiiType type, MaskingStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(value);
        return strategy switch
        {
            MaskingStrategy.Keep => value,
            MaskingStrategy.Null => string.Empty,
            MaskingStrategy.HmacToken => Token(value, type),
            MaskingStrategy.FormatPreserving => FormatPreserving(value, type),
            MaskingStrategy.Partial => Partial(value, type),
            MaskingStrategy.Generalize => Generalize(value, type),
            MaskingStrategy.Synthetic => Synthetic(value, type),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
        };
    }

    /// <summary>Opaque token: <c>tok_</c> + 16 base32 characters (80 bits) of HMAC output.</summary>
    public string Token(string value, PiiType type)
    {
        var prf = Prf(value, type);
        var builder = new StringBuilder("tok_", 20);
        var seed = prf.Seed;
        for (var i = 0; i < 16; i++)
        {
            builder.Append(TokenAlphabet[seed[i] & 31]);
        }

        return builder.ToString();
    }

    private string FormatPreserving(string value, PiiType type)
    {
        var prf = Prf(value, type);
        switch (type)
        {
            case PiiType.NationalId:
            {
                Span<char> nine = stackalloc char[9];
                nine[0] = prf.NonZeroDigit();
                for (var i = 1; i < 9; i++)
                {
                    nine[i] = prf.Digit();
                }

                var (d10, d11) = Checksums.TcknCheckDigits(nine);
                return string.Concat(nine, ((char)('0' + d10)).ToString(), ((char)('0' + d11)).ToString());
            }

            case PiiType.TaxId:
            {
                Span<char> nine = stackalloc char[9];
                for (var i = 0; i < 9; i++)
                {
                    nine[i] = prf.Digit();
                }

                return string.Concat(nine, ((char)('0' + Checksums.VknCheckDigit(nine))).ToString());
            }

            case PiiType.Iban:
                return MaskIban(value, prf);

            case PiiType.PaymentCard:
                return MaskCard(value, prf, keepLast4: false);

            case PiiType.Phone:
            {
                // Keep the shape and the first three national digits (operator / area code); replace the subscriber number.
                var digitCount = value.Count(char.IsAsciiDigit);
                var keep = Math.Max(0, digitCount - 7);
                return ReplaceDigits(value, prf, keep);
            }

            case PiiType.Email:
            {
                var at = value.IndexOf('@', StringComparison.Ordinal);
                var localLength = Math.Clamp(at > 0 ? at : 8, 6, 16);
                var local = new StringBuilder(localLength);
                for (var i = 0; i < localLength; i++)
                {
                    local.Append(prf.Letter(i == 0 ? LowerLetters : LowerLetters + "0123456789"));
                }

                // RFC 2606 reserved domain: masked addresses can never deliver mail to a real mailbox.
                return local + "@example.invalid";
            }

            case PiiType.VehiclePlate:
            {
                var builder = new StringBuilder(value.Length);
                for (var i = 0; i < value.Length; i++)
                {
                    var ch = value[i];
                    builder.Append(i < 2 && char.IsAsciiDigit(ch) ? ch
                        : char.IsAsciiDigit(ch) ? prf.Digit()
                        : char.IsLetter(ch) ? prf.Letter(PlateLetters)
                        : ch);
                }

                return builder.ToString();
            }

            default:
                return PreserveCharacterClasses(value, prf);
        }
    }

    private static string Partial(string value, PiiType type)
    {
        if (type == PiiType.Email)
        {
            var at = value.IndexOf('@', StringComparison.Ordinal);
            return at > 0 ? value[0] + "***" + value[at..] : "***";
        }

        if (type == PiiType.PaymentCard)
        {
            return MaskCard(value, prf: null, keepLast4: true);
        }

        var visible = 0;
        var chars = value.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                continue;
            }

            if (visible < 4 && chars.Length > 6)
            {
                visible++;
            }
            else
            {
                chars[i] = '*';
            }
        }

        return new string(chars);
    }

    private string Generalize(string value, PiiType type)
    {
        switch (type)
        {
            case PiiType.DateOfBirth when value.Length >= 4 && int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year):
                return year.ToString("0000", CultureInfo.InvariantCulture) + "-01-01";

            case PiiType.IpAddress when value.Count(c => c == '.') == 3:
                return value[..value.LastIndexOf('.')] + ".0";

            case PiiType.IpAddress when value.Contains(':', StringComparison.Ordinal) && System.Net.IPAddress.TryParse(value, out var ip):
            {
                var bytes = ip.GetAddressBytes();
                Array.Clear(bytes, 6, bytes.Length - 6); // keep the /48 routing prefix
                return new System.Net.IPAddress(bytes).ToString();
            }

            case PiiType.Address:
            {
                var folded = TurkishText.Fold(value);
                var province = Provinces.LastOrDefault(p => folded.Contains(p, StringComparison.Ordinal));
                return province is null ? "***" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(province);
            }

            case PiiType.Salary when decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount):
                return (Math.Floor(amount / 5000m) * 5000m).ToString("0", CultureInfo.InvariantCulture);

            default:
                return Token(value, type);
        }
    }

    private string Synthetic(string value, PiiType type)
    {
        var prf = Prf(value, type);
        switch (type)
        {
            case PiiType.PersonName:
            {
                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var name = words <= 1
                    ? Capitalise(FirstNames[prf.Next(FirstNames.Length)])
                    : Capitalise(FirstNames[prf.Next(FirstNames.Length)]) + " " + Capitalise(Surnames[prf.Next(Surnames.Length)]);
                return value.Any(char.IsLower) ? name : name.ToUpperInvariant();
            }

            case PiiType.Address:
            {
                var street = Streets[prf.Next(Streets.Length)];
                var neighbourhood = Streets[prf.Next(Streets.Length)];
                var number = prf.Next(200) + 1;
                var province = Capitalise(Provinces[prf.Next(Provinces.Length)]);
                return string.Create(CultureInfo.InvariantCulture, $"{neighbourhood} Mah. {street} Sok. No:{number} {province}");
            }

            default:
                return FormatPreserving(value, type);
        }
    }

    private static string MaskIban(string value, Pseudorandom prf)
    {
        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (compact.Length < 15 || !char.IsAsciiLetter(compact[0]) || !char.IsAsciiLetter(compact[1]))
        {
            return PreserveCharacterClasses(value, prf);
        }

        // Keep country, bank code (5) and reserve digit; regenerate the account part and the check digits.
        var keep = compact.StartsWith("TR", StringComparison.Ordinal) ? 6 : 4;
        var bban = new StringBuilder(compact[4..(4 + keep)]);
        for (var i = 4 + keep; i < compact.Length; i++)
        {
            bban.Append(char.IsAsciiDigit(compact[i]) ? prf.Digit() : prf.Letter(UpperLetters));
        }

        var country = compact[..2];
        var masked = country + Checksums.IbanCheckDigits(country, bban.ToString()) + bban;
        return value.Contains(' ', StringComparison.Ordinal) ? Group(masked, 4) : masked;
    }

    private static string MaskCard(string value, Pseudorandom? prf, bool keepLast4)
    {
        var digits = TurkishText.DigitsOnly(value);
        if (digits.Length < 12)
        {
            return new string('*', value.Length);
        }

        string replacement;
        if (keepLast4)
        {
            replacement = new string('*', digits.Length - 4) + digits[^4..];
        }
        else
        {
            // Keep the issuer prefix (first 6, the BIN), regenerate the rest, recompute the Luhn digit.
            var body = new StringBuilder(digits[..6]);
            for (var i = 6; i < digits.Length - 1; i++)
            {
                body.Append(prf!.Digit());
            }

            body.Append((char)('0' + Checksums.LuhnCheckDigit(body.ToString())));
            replacement = body.ToString();
        }

        // Re-insert the original separators.
        var result = new StringBuilder(value.Length);
        var k = 0;
        foreach (var ch in value)
        {
            result.Append(char.IsAsciiDigit(ch) ? replacement[k++] : ch);
        }

        return result.ToString();
    }

    private static string ReplaceDigits(string value, Pseudorandom prf, int keepLeadingDigits)
    {
        var builder = new StringBuilder(value.Length);
        var seen = 0;
        foreach (var ch in value)
        {
            if (char.IsAsciiDigit(ch))
            {
                builder.Append(seen++ < keepLeadingDigits ? ch : prf.Digit());
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string PreserveCharacterClasses(string value, Pseudorandom prf)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsAsciiDigit(ch) ? prf.Digit()
                : char.IsUpper(ch) ? prf.Letter(UpperLetters)
                : char.IsLetter(ch) ? prf.Letter(LowerLetters)
                : ch);
        }

        return builder.ToString();
    }

    private DateTime ShiftDate(DateTime date)
    {
        // Keyed shift of up to ±180 days: keeps the age distribution, breaks the exact date.
        var prf = Prf(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), PiiType.DateOfBirth);
        var days = ((prf.Next(256) << 8) | prf.Next(256)) % 361 - 180;
        return date.AddDays(days);
    }

    private Pseudorandom Prf(string value, PiiType type) => new(_key, type.ToString(), Normalize(value, type));

    internal static string Normalize(string value, PiiType type) => type switch
    {
        PiiType.NationalId or PiiType.TaxId or PiiType.PaymentCard => TurkishText.DigitsOnly(value),
        PiiType.Phone => NormalizePhone(value),
        PiiType.Iban => value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(),
        PiiType.Email => value.Trim().ToLowerInvariant(),
        PiiType.PersonName or PiiType.Address => string.Join(' ', TurkishText.Fold(value).Split(' ', StringSplitOptions.RemoveEmptyEntries)),
        _ => value.Trim(),
    };

    private static string NormalizePhone(string value)
    {
        var digits = TurkishText.DigitsOnly(value);
        return digits.Length switch
        {
            14 when digits.StartsWith("0090", StringComparison.Ordinal) => digits[4..],
            12 when digits.StartsWith("90", StringComparison.Ordinal) => digits[2..],
            11 when digits[0] == '0' => digits[1..],
            _ => digits,
        };
    }

    private static string Group(string value, int size)
    {
        var builder = new StringBuilder(value.Length + (value.Length / size));
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && i % size == 0)
            {
                builder.Append(' ');
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static string Capitalise(string folded) =>
        folded.Length == 0 ? folded : char.ToUpperInvariant(folded[0]) + folded[1..];

    private static object? ConvertBack(string masked, object original) => original switch
    {
        string => masked,
        decimal => decimal.TryParse(masked, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : null,
        long => long.TryParse(masked, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null,
        int => int.TryParse(masked, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
        double => double.TryParse(masked, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
        DateTime => DateTime.TryParse(masked, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null,
        // A masked value that no longer fits the column type is dropped rather than half-masked.
        _ => null,
    };
}
