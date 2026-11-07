using System.Globalization;
using KvkkInventory.Classification;
using KvkkInventory.Text;

namespace KvkkInventory.Detection;

/// <summary>
/// Detects one <see cref="PiiType"/> in a column by combining two independent kinds of evidence, the approach
/// Microsoft Presidio uses for free text (pattern → validator → context words), adapted to columns:
/// <list type="bullet">
/// <item><b>name evidence</b>: the column name (and comment) matches a Turkish or English rule;</item>
/// <item><b>value evidence</b>: the share of sampled values that match the pattern <i>and</i> pass the validator
/// (checksum, plausible range), scaled by how much a match can be trusted (<see cref="ValueStrength"/>).</item>
/// </list>
/// They are combined with a noisy-OR, so either source alone can reach the threshold when it is strong, and the
/// two together reinforce each other. A value-checkable type whose samples contain no match at all is
/// <i>contradicted</i>: a column named <c>email_verified</c> holding booleans is not e-mail.
/// </summary>
public abstract class Recognizer
{
    /// <summary>How much name evidence alone counts when the type can also be checked on values.</summary>
    protected const double NameFactor = 0.6;

    /// <summary>How much name evidence counts for types that cannot be checked on values (religion, union …).</summary>
    protected const double NameOnlyFactor = 0.9;

    private const double ContradictionPenalty = 0.35;
    private const double NegativeContextPenalty = 0.3;
    private const double CommentDiscount = 0.8;
    private const int MinSamplesForContradiction = 10;

    // Tokens that mean "derived from" or "about" the value rather than the value itself.
    private static readonly HashSet<string> DerivedTokens =
    [
        "hash", "sha256", "md5", "count", "sayisi", "adet", "verified", "dogrulandi", "flag", "tipi", "turu", "type",
        "format", "mask", "maskeli", "masked", "policy", "politikasi",
    ];

    public abstract PiiType Type { get; }

    /// <summary>Trust in a validated value match (0 = this type is detected from names only).</summary>
    public virtual double ValueStrength => 0;

    protected abstract IReadOnlyList<NameRule> NameRules { get; }

    /// <summary>Column-name tokens that point to a non-person entity for this type (e.g. <c>ilac_adi</c> is a drug name).</summary>
    protected virtual IReadOnlySet<string> NegativeTokens => EmptySet;

    private static IReadOnlySet<string> EmptySet { get; } = new HashSet<string>();

    public Evidence? Evaluate(ColumnContext column, DetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(options);

        var (nameScore, rule) = options.UseNames ? NameScore(column) : (0, null);
        var checksValues = ValueStrength > 0 && options.UseValues && !column.IsBinary;

        double valueScore = 0;
        int matched = 0, sampled = column.Samples.Count;
        var contradicted = false;
        if (checksValues && sampled > 0)
        {
            (valueScore, matched) = ScoreValues(column.Samples);
            contradicted = sampled >= MinSamplesForContradiction && matched <= sampled * 0.02;
        }

        var effectiveName = contradicted ? nameScore * ContradictionPenalty : nameScore;
        var confidence = ValueStrength > 0
            ? 1 - ((1 - valueScore) * (1 - (NameFactor * effectiveName)))
            : NameOnlyFactor * effectiveName;

        if (confidence <= 0)
        {
            return null;
        }

        var reason = string.Create(CultureInfo.InvariantCulture,
            $"name={nameScore:F2}{(rule is null ? string.Empty : $" ('{rule}')")}; values={matched}/{sampled} → {valueScore:F2}{(contradicted ? "; contradicted by values" : string.Empty)}");
        return new Evidence(Type, Math.Round(confidence, 4), Math.Round(nameScore, 4), Math.Round(valueScore, 4), matched, sampled, reason);
    }

    /// <summary>Matches a single value. Implementations validate, not just pattern-match.</summary>
    protected virtual ValueMatch MatchValue(string value) => ValueMatch.None;

    /// <summary>
    /// Column-level value evidence. Structured columns need most values to match (saturating at 80 %, so a few
    /// dirty rows do not hide a TCKN column); free-text columns count embedded matches and saturate at 25 %,
    /// because a notes column that mentions a phone number in every fourth row is a phone-number store.
    /// </summary>
    protected virtual (double Score, int Matched) ScoreValues(IReadOnlyList<string> samples)
    {
        int full = 0, embedded = 0;
        foreach (var sample in samples)
        {
            switch (MatchValue(sample))
            {
                case ValueMatch.Full:
                    full++;
                    break;
                case ValueMatch.Embedded:
                    embedded++;
                    break;
            }
        }

        double n = samples.Count;
        var ratio = IsFreeText(samples)
            ? Math.Min(1, (full + embedded) / n / 0.25) * 0.85
            : Math.Min(1, full / n / 0.8);

        // Very small samples are weak evidence whatever they contain.
        var sizeFactor = Math.Min(1, n / 5);
        return (ValueStrength * ratio * sizeFactor, full + embedded);
    }

    /// <summary>Free text = mostly multi-word values of sentence length.</summary>
    protected static bool IsFreeText(IReadOnlyList<string> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var sentences = samples.Count(s => s.Length >= 25 && s.Count(char.IsWhiteSpace) >= 3);
        return sentences * 2 > samples.Count;
    }

    private (double Score, string? Rule) NameScore(ColumnContext column)
    {
        var tokens = TurkishText.Tokenize(column.Column);
        var compact = string.Concat(tokens);
        double best = 0;
        string? bestRule = null;

        foreach (var rule in NameRules)
        {
            if (rule.Weight > best && rule.Matches(tokens, compact))
            {
                (best, bestRule) = (rule.Weight, rule.Pattern);
            }
        }

        if (!string.IsNullOrWhiteSpace(column.Comment))
        {
            var commentTokens = TurkishText.Tokenize(column.Comment);
            var commentCompact = string.Concat(commentTokens);
            foreach (var rule in NameRules)
            {
                var weight = rule.Weight * CommentDiscount;
                if (weight > best && rule.Matches(commentTokens, commentCompact))
                {
                    (best, bestRule) = (weight, $"comment: {rule.Pattern}");
                }
            }
        }

        if (best > 0 && tokens.Any(t => DerivedTokens.Contains(t) || NegativeTokens.Contains(t)))
        {
            best *= NegativeContextPenalty;
        }

        return (best, bestRule);
    }

    protected static NameRule[] Rules(params (string Pattern, double Weight)[] rules) =>
        [.. rules.Select(r => new NameRule(r.Pattern, r.Weight))];
}
