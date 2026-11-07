using KvkkInventory.Text;

namespace KvkkInventory.Detection;

/// <summary>
/// A column-name pattern such as <c>"dogum tarihi"</c> with the weight of the evidence it provides.
/// A rule matches when its tokens appear contiguously in the column's tokens (a token may carry a Turkish
/// possessive/plural suffix: <c>adres</c> matches <c>adresi</c>), or when the rule's concatenation (≥ 5 letters)
/// appears inside the concatenated column name (<c>tckimlikno</c>, <c>DOGUMTARIHI</c>).
/// </summary>
public sealed class NameRule
{
    // Suffixes that keep the meaning of the noun: possessive (-i, -u, -si, -su), plural (-ler, -lar), genitive.
    private static readonly string[] Suffixes = ["i", "u", "si", "su", "leri", "lari", "ler", "lar", "in", "un", "nin", "nun"];

    private readonly string[] _tokens;
    private readonly string _compact;

    public NameRule(string pattern, double weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        _tokens = [.. TurkishText.Tokenize(pattern)];
        _compact = string.Concat(_tokens);
        Pattern = pattern;
        Weight = weight;
    }

    public string Pattern { get; }

    public double Weight { get; }

    public bool Matches(IReadOnlyList<string> tokens, string compact)
    {
        for (var start = 0; start + _tokens.Length <= tokens.Count; start++)
        {
            var all = true;
            for (var j = 0; j < _tokens.Length && all; j++)
            {
                all = TokenMatches(tokens[start + j], _tokens[j]);
            }

            if (all)
            {
                return true;
            }
        }

        return _compact.Length >= 5 && compact.Contains(_compact, StringComparison.Ordinal);
    }

    private static bool TokenMatches(string actual, string expected)
    {
        if (actual == expected)
        {
            return true;
        }

        if (expected.Length < 3 || !actual.StartsWith(expected, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = actual.AsSpan(expected.Length);
        foreach (var suffix in Suffixes)
        {
            if (rest.SequenceEqual(suffix))
            {
                return true;
            }
        }

        return false;
    }
}
