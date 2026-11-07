using System.Diagnostics;
using System.Runtime.CompilerServices;
using KvkkInventory.Schema;

namespace KvkkInventory.Masking;

public sealed record TableMaskingResult(string Table, long Rows, int MaskedColumns);

public sealed record MaskingSummary(string SourceSchema, string TargetSchema, double DurationMs, IReadOnlyList<TableMaskingResult> Tables)
{
    public long Rows => Tables.Sum(t => t.Rows);
}

/// <summary>
/// Copy-to-masked-schema: every table of the source schema is recreated empty in the target schema and filled
/// with masked rows streamed from the source. The source is only read, never updated, so a failed or interrupted
/// run cannot leave production half-masked; tables without personal data are copied unchanged so the target is a
/// complete, consistent test database.
/// </summary>
public sealed class MaskingRunner(IRelationalDatabase database, Masker masker)
{
    public static readonly ActivitySource ActivitySource = new("KvkkInventory.Mask");

    public async Task<MaskingSummary> RunAsync(string sourceSchema, string targetSchema, MaskingPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.Equals(sourceSchema, targetSchema, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The masked copy must go to a different schema than the source.", nameof(targetSchema));
        }

        using var activity = ActivitySource.StartActivity("mask");
        var watch = Stopwatch.StartNew();
        var results = new List<TableMaskingResult>();

        foreach (var table in await database.ReadCatalogAsync(sourceSchema, cancellationToken))
        {
            var mask = plan.For(table.Name);
            var byIndex = table.Columns
                .Select(column => mask?.Columns.FirstOrDefault(m => string.Equals(m.Column, column.Name, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            var nullable = byIndex.Where(m => m?.Strategy == MaskingStrategy.Null).Select(m => m!.Column).ToArray();

            await database.DropTableIfExistsAsync(targetSchema, table.Name, cancellationToken);
            await database.CreateEmptyCopyAsync(table, targetSchema, nullable, cancellationToken);

            var rows = await database.InsertAsync(targetSchema, table.Name, [.. table.Columns.Select(c => c.Name)],
                Transform(table, byIndex, cancellationToken), cancellationToken);
            results.Add(new TableMaskingResult(table.Name, rows, byIndex.Count(m => m is not null && m.Strategy != MaskingStrategy.Keep)));
        }

        return new MaskingSummary(sourceSchema, targetSchema, Math.Round(watch.Elapsed.TotalMilliseconds, 1), results);
    }

    private async IAsyncEnumerable<object?[]> Transform(TableInfo table, ColumnMask?[] byIndex, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in database.ReadAllAsync(table, cancellationToken))
        {
            for (var i = 0; i < row.Length; i++)
            {
                if (byIndex[i] is { } mask)
                {
                    row[i] = masker.Mask(row[i], mask.Type, mask.Strategy, table.Columns[i].MaxLength);
                }
            }

            yield return row;
        }
    }
}
