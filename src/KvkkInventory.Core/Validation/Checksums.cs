using System.Globalization;

namespace KvkkInventory.Validation;

/// <summary>
/// Check-digit algorithms for Turkish and international identifiers. A checksum turns a weak pattern
/// ("11 digits") into strong evidence: a random 11-digit number passes the TCKN check with probability ≈ 1/100,
/// a random IBAN-shaped string passes mod-97 with probability ≈ 1/97.
/// </summary>
public static class Checksums
{
    /// <summary>
    /// T.C. Kimlik No (TCKN, Turkish national identity number): 11 digits, first digit non-zero,
    /// d10 = ((d1+d3+d5+d7+d9)·7 − (d2+d4+d6+d8)) mod 10, d11 = (d1+…+d10) mod 10.
    /// Foreign residents' numbers (YKN, starting with 99) use the same algorithm.
    /// </summary>
    public static bool IsValidTckn(ReadOnlySpan<char> value)
    {
        if (value.Length != 11 || value[0] == '0' || !AllDigits(value))
        {
            return false;
        }

        var (d10, d11) = TcknCheckDigits(value[..9]);
        return value[9] - '0' == d10 && value[10] - '0' == d11;
    }

    /// <summary>Computes the two TCKN check digits for the first nine digits.</summary>
    public static (int D10, int D11) TcknCheckDigits(ReadOnlySpan<char> firstNine)
    {
        if (firstNine.Length != 9 || !AllDigits(firstNine))
        {
            throw new ArgumentException("Exactly nine digits are required.", nameof(firstNine));
        }

        int odd = 0, even = 0;
        for (var i = 0; i < 9; i++)
        {
            var d = firstNine[i] - '0';
            if (i % 2 == 0)
            {
                odd += d;
            }
            else
            {
                even += d;
            }
        }

        var d10 = ((((odd * 7) - even) % 10) + 10) % 10;
        var d11 = (odd + even + d10) % 10;
        return (d10, d11);
    }

    /// <summary>
    /// Vergi Kimlik No (VKN, tax identification number of legal entities and sole proprietors): 10 digits; the
    /// last digit is a check digit over the first nine (the algorithm python-stdnum implements in <c>stdnum.tr.vkn</c>).
    /// </summary>
    public static bool IsValidVkn(ReadOnlySpan<char> value) =>
        value.Length == 10 && AllDigits(value) && value[9] - '0' == VknCheckDigit(value[..9]);

    /// <summary>Computes the VKN check digit for the first nine digits.</summary>
    public static int VknCheckDigit(ReadOnlySpan<char> firstNine)
    {
        if (firstNine.Length != 9 || !AllDigits(firstNine))
        {
            throw new ArgumentException("Exactly nine digits are required.", nameof(firstNine));
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var t = (firstNine[i] - '0' + 9 - i) % 10;
            var c = t == 9 ? 9 : t * (1 << (9 - i)) % 9;
            sum += c;
        }

        return (10 - (sum % 10)) % 10;
    }

    /// <summary>ISO 13616 IBAN check: move the first four characters to the end, map letters to 10..35, mod 97 = 1.</summary>
    public static bool IsValidIban(ReadOnlySpan<char> compact)
    {
        if (compact.Length is < 15 or > 34 || !char.IsAsciiLetterUpper(compact[0]) || !char.IsAsciiLetterUpper(compact[1])
            || !char.IsAsciiDigit(compact[2]) || !char.IsAsciiDigit(compact[3]))
        {
            return false;
        }

        // Türkiye: TR + 2 check digits + 5-digit bank code + 1 reserve digit + 16-digit account = 26 characters.
        if (compact[0] == 'T' && compact[1] == 'R' && (compact.Length != 26 || !AllDigits(compact[2..])))
        {
            return false;
        }

        return Mod97(compact[4..], compact[..4]) == 1;
    }

    /// <summary>Computes the two IBAN check digits for a country code and BBAN.</summary>
    public static string IbanCheckDigits(string countryCode, string bban)
    {
        ArgumentNullException.ThrowIfNull(countryCode);
        ArgumentNullException.ThrowIfNull(bban);
        var remainder = Mod97(bban, countryCode + "00");
        return (98 - remainder).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Luhn (mod 10) check used by payment card numbers (PAN), 13–19 digits.</summary>
    public static bool IsValidLuhn(ReadOnlySpan<char> digits) =>
        digits.Length is >= 13 and <= 19 && AllDigits(digits) && LuhnSum(digits, payloadOnly: false) % 10 == 0;

    /// <summary>Computes the Luhn check digit to append to <paramref name="payload"/>.</summary>
    public static int LuhnCheckDigit(ReadOnlySpan<char> payload) => (10 - (LuhnSum(payload, payloadOnly: true) % 10)) % 10;

    // payloadOnly: the check digit is not present yet, so the doubling starts at the rightmost payload digit.
    private static int LuhnSum(ReadOnlySpan<char> digits, bool payloadOnly)
    {
        var sum = 0;
        var doubleIt = payloadOnly;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum;
    }

    private static int Mod97(ReadOnlySpan<char> head, ReadOnlySpan<char> tail)
    {
        var remainder = Accumulate(0, head);
        return remainder < 0 ? -1 : Accumulate(remainder, tail);

        static int Accumulate(int remainder, ReadOnlySpan<char> part)
        {
            foreach (var ch in part)
            {
                if (char.IsAsciiDigit(ch))
                {
                    remainder = ((remainder * 10) + (ch - '0')) % 97;
                }
                else if (char.IsAsciiLetterUpper(ch))
                {
                    remainder = ((remainder * 100) + (ch - 'A' + 10)) % 97;
                }
                else
                {
                    return -1;
                }
            }

            return remainder;
        }
    }

    private static bool AllDigits(ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}
