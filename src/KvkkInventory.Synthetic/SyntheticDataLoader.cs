using Bogus;
using KvkkInventory.Schema;

namespace KvkkInventory.Synthetic;

public sealed record LoadResult(int Tables, long Rows, double DurationMs);

/// <summary>
/// Creates the synthetic tables on any <see cref="IRelationalDatabase"/> and fills them with deterministic rows
/// (same seed → same data on Oracle, PostgreSQL and in memory, which is what makes cross-engine parity testable).
/// </summary>
public sealed class SyntheticDataLoader(IRelationalDatabase database)
{
    public async Task<LoadResult> LoadAsync(
        string schema,
        IReadOnlyList<SyntheticTable> tables,
        int rowsPerTable,
        int seed = 20261107,
        string? grantSelectTo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long total = 0;

        for (var t = 0; t < tables.Count; t++)
        {
            var table = tables[t];
            await database.DropTableIfExistsAsync(schema, table.Name, cancellationToken);
            await database.CreateTableAsync(schema, table.Spec, cancellationToken);
            total += await database.InsertAsync(schema, table.Name, [.. table.Columns.Select(c => c.Spec.Name)],
                Generate(table, rowsPerTable, seed + (t * 7919)), cancellationToken);
            if (grantSelectTo is not null)
            {
                await database.GrantSelectAsync(schema, table.Name, grantSelectTo, cancellationToken);
            }
        }

        return new LoadResult(tables.Count, total, Math.Round(watch.Elapsed.TotalMilliseconds, 1));
    }

    /// <summary>Rows for one table; each table has its own seeded faker so tables are independent of load order.</summary>
    public static IEnumerable<object?[]> Generate(SyntheticTable table, int rows, int seed)
    {
        ArgumentNullException.ThrowIfNull(table);
        var faker = new Faker("tr") { Random = new Randomizer(seed) };
        for (var i = 0; i < rows; i++)
        {
            var context = new RowContext(faker, i);
            var row = new object?[table.Columns.Count];
            for (var c = 0; c < row.Length; c++)
            {
                row[c] = table.Columns[c].Generate(context);
            }

            yield return row;
        }
    }
}
