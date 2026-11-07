using System.Text;

namespace KvkkInventory.Text;

/// <summary>
/// Culture-independent folding of Turkish text. Column names and sampled values are compared after folding, so
/// "DOĞUM_TARİHİ", "dogum_tarihi" and "DogumTarihi" all reach the same detector rule. The mapping is explicit
/// instead of relying on <c>tr-TR</c> casing because services run with invariant globalization (no ICU).
/// </summary>
public static class TurkishText
{
    /// <summary>Lower-cases and folds Turkish letters to ASCII: İ/I/ı→i, Ğ/ğ→g, Ü/ü→u, Ş/ş→s, Ö/ö→o, Ç/ç→c.</summary>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                'İ' or 'I' or 'ı' => 'i',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ş' or 'ş' => 's',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                'Â' or 'â' => 'a',
                'Î' or 'î' => 'i',
                'Û' or 'û' => 'u',
                _ => char.ToLowerInvariant(ch),
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Splits an identifier into folded tokens on separators and camelCase boundaries:
    /// <c>"hastaTcKimlikNo"</c> → <c>hasta, tc, kimlik, no</c>; <c>"DOGUM_TARIHI"</c> → <c>dogum, tarihi</c>.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var tokens = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < identifier.Length; i++)
        {
            var ch = identifier[i];
            if (!char.IsLetterOrDigit(ch))
            {
                Flush();
                continue;
            }

            // camelCase boundary (lower→Upper) or letter↔digit edge. ALLCAPS Oracle names split only on separators.
            if (current.Length > 0)
            {
                var previous = identifier[i - 1];
                var camel = char.IsLower(previous) && char.IsUpper(ch);
                var digitEdge = char.IsDigit(previous) != char.IsDigit(ch);
                if (camel || digitEdge)
                {
                    Flush();
                }
            }

            current.Append(ch);
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(Fold(current.ToString()));
                current.Clear();
            }
        }
    }

    /// <summary>Keeps only ASCII digits, e.g. "+90 (532) 111-22-33" → "905321112233".</summary>
    public static string DigitsOnly(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsAsciiDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
