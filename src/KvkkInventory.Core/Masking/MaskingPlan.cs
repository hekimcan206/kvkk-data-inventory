using KvkkInventory.Classification;

namespace KvkkInventory.Masking;

public sealed record ColumnMask(string Column, PiiType Type, MaskingStrategy Strategy);

public sealed record TableMask(string Table, IReadOnlyList<ColumnMask> Columns);

/// <summary>
/// Which strategy applies to which column. One strategy per column: when a column carries several types, a
/// special category wins (strictest), otherwise the most confident type. Overrides come from the masking policy
/// file as <c>"TABLE.COLUMN": "Strategy"</c> or <c>"PiiType": "Strategy"</c>.
/// </summary>
public sealed record MaskingPlan(IReadOnlyList<TableMask> Tables)
{
    public static MaskingPlan From(ClassificationReport classification, IReadOnlyDictionary<string, MaskingStrategy>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(classification);
        overrides ??= new Dictionary<string, MaskingStrategy>();
        var lookup = new Dictionary<string, MaskingStrategy>(overrides, StringComparer.OrdinalIgnoreCase);

        var tables = classification.Columns
            .GroupBy(c => c.Table, StringComparer.OrdinalIgnoreCase)
            .Select(table => new TableMask(table.Key, [.. table
                .GroupBy(c => c.Column, StringComparer.OrdinalIgnoreCase)
                .Select(column =>
                {
                    var primary = column.OrderByDescending(c => c.Special).ThenByDescending(c => c.Confidence).First();
                    var strategy = lookup.TryGetValue($"{table.Key}.{column.Key}", out var byColumn) ? byColumn
                        : lookup.TryGetValue(primary.Type.ToString(), out var byType) ? byType
                        : primary.Masking;
                    return new ColumnMask(column.Key, primary.Type, strategy);
                })]))
            .ToArray();
        return new MaskingPlan(tables);
    }

    public TableMask? For(string table) =>
        Tables.FirstOrDefault(t => string.Equals(t.Table, table, StringComparison.OrdinalIgnoreCase));
}
