using System.Globalization;
using System.Text;
using KvkkInventory.Classification;

namespace KvkkInventory.Evaluation;

/// <summary>Ground truth for one column: the set of PII types it really contains (empty = not personal data).</summary>
public sealed record LabelledColumn(string Table, string Column, IReadOnlySet<PiiType> Types);

public sealed record DetectorMetrics(PiiType Type, int Support, int TruePositives, int FalsePositives, int FalseNegatives)
{
    public double Precision => TruePositives + FalsePositives == 0 ? double.NaN : TruePositives / (double)(TruePositives + FalsePositives);

    public double Recall => TruePositives + FalseNegatives == 0 ? double.NaN : TruePositives / (double)(TruePositives + FalseNegatives);

    public double F1 => double.IsNaN(Precision) || double.IsNaN(Recall) || Precision + Recall == 0 ? 0 : 2 * Precision * Recall / (Precision + Recall);
}

public sealed record EvaluationResult(int Columns, IReadOnlyList<DetectorMetrics> PerType, DetectorMetrics Micro, IReadOnlyList<string> Errors)
{
    public double MacroF1 => PerType.Where(m => m.Support > 0).Average(m => m.F1);
}

/// <summary>
/// Column-level, multi-label evaluation: for each PII type, a column is a true positive when it is labelled and
/// classified as that type, a false positive when classified but not labelled, a false negative when labelled but
/// not classified. Micro-averaged totals weight every column decision equally.
/// </summary>
public static class Evaluator
{
    public static EvaluationResult Evaluate(ClassificationReport classification, IReadOnlyList<LabelledColumn> truth)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(truth);
        var predicted = classification.Labels();
        var counts = Enum.GetValues<PiiType>().ToDictionary(t => t, _ => (Support: 0, Tp: 0, Fp: 0, Fn: 0));
        var errors = new List<string>();

        foreach (var column in truth)
        {
            var key = (column.Table.ToUpperInvariant(), column.Column.ToUpperInvariant());
            var got = predicted[key].ToHashSet();
            foreach (var type in Enum.GetValues<PiiType>())
            {
                var expected = column.Types.Contains(type);
                var actual = got.Contains(type);
                var c = counts[type];
                counts[type] = (c.Support + (expected ? 1 : 0), c.Tp + (expected && actual ? 1 : 0), c.Fp + (!expected && actual ? 1 : 0), c.Fn + (expected && !actual ? 1 : 0));
                if (expected != actual)
                {
                    errors.Add($"{(actual ? "FP" : "FN")} {type,-22} {column.Table}.{column.Column}");
                }
            }
        }

        var perType = counts.Select(kv => new DetectorMetrics(kv.Key, kv.Value.Support, kv.Value.Tp, kv.Value.Fp, kv.Value.Fn)).ToArray();
        var micro = new DetectorMetrics(default, perType.Sum(m => m.Support), perType.Sum(m => m.TruePositives),
            perType.Sum(m => m.FalsePositives), perType.Sum(m => m.FalseNegatives));
        return new EvaluationResult(truth.Count, perType, micro, errors);
    }

    public static string ToMarkdown(EvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var md = new StringBuilder();
        md.Append("| Detector | Support | TP | FP | FN | Precision | Recall | F1 |\n|---|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (var m in result.PerType.Where(m => m.Support > 0 || m.FalsePositives > 0))
        {
            md.Append(CultureInfo.InvariantCulture, $"| {m.Type} | {m.Support} | {m.TruePositives} | {m.FalsePositives} | {m.FalseNegatives} | {F(m.Precision)} | {F(m.Recall)} | {F(m.F1)} |\n");
        }

        var micro = result.Micro;
        md.Append(CultureInfo.InvariantCulture, $"| **micro avg** | {micro.Support} | {micro.TruePositives} | {micro.FalsePositives} | {micro.FalseNegatives} | **{F(micro.Precision)}** | **{F(micro.Recall)}** | **{F(micro.F1)}** |\n");
        md.Append(CultureInfo.InvariantCulture, $"| macro F1 | | | | | | | {F(result.MacroF1)} |\n");
        return md.ToString();
    }

    private static string F(double value) => double.IsNaN(value) ? "n/a" : value.ToString("0.000", CultureInfo.InvariantCulture);
}
