using Bogus;
using KvkkInventory.Classification;
using KvkkInventory.Evaluation;
using KvkkInventory.Schema;

namespace KvkkInventory.Synthetic;

/// <summary>What a column generator gets: the seeded faker and the row number.</summary>
public sealed class RowContext(Faker faker, int index)
{
    public Faker F { get; } = faker;

    public Randomizer R => F.Random;

    public int Index { get; } = index;
}

/// <summary>A column with its generator and its ground-truth label set (empty = not personal data).</summary>
public sealed record SyntheticColumn(ColumnSpec Spec, IReadOnlySet<PiiType> Labels, Func<RowContext, object?> Generate);

public sealed record SyntheticTable(string Name, IReadOnlyList<SyntheticColumn> Columns, string? Comment = null)
{
    public TableSpec Spec => new(Name, [.. Columns.Select(c => c.Spec)], Comment);

    public SyntheticTable WithSuffix(string suffix) => this with { Name = Name + suffix };
}

public static class SyntheticSchemaExtensions
{
    /// <summary>The ground truth the evaluation compares the classifier's output with.</summary>
    public static IReadOnlyList<LabelledColumn> GroundTruth(this IEnumerable<SyntheticTable> tables) =>
        [.. tables.SelectMany(t => t.Columns.Select(c => new LabelledColumn(t.Name, c.Spec.Name, c.Labels)))];
}
