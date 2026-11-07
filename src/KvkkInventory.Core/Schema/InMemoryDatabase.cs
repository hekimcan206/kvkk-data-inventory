using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace KvkkInventory.Schema;

/// <summary>
/// A dialect-free database held in memory. It lets the detection/evaluation pipeline run without Docker (unit
/// tests, the API's dry-run) and is the reference the Oracle and PostgreSQL scans are compared against.
/// </summary>
public sealed class InMemoryDatabase : IRelationalDatabase
{
    private readonly ConcurrentDictionary<string, (TableSpec Spec, List<object?[]> Rows)> _tables = new(StringComparer.OrdinalIgnoreCase);

    public string Dialect => "memory";

    public Task<IReadOnlyList<TableInfo>> ReadCatalogAsync(string schema, CancellationToken cancellationToken = default)
    {
        var prefix = schema + ".";
        IReadOnlyList<TableInfo> tables = [.. _tables
            .Where(t => t.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .Select(t => ToInfo(schema, t.Value.Spec, t.Value.Rows.Count))];
        return Task.FromResult(tables);
    }

    public Task<IReadOnlyList<object?[]>> SampleAsync(TableInfo table, IReadOnlyList<ColumnInfo> columns, int rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);
        var (spec, data) = _tables[Key(table.Schema, table.Name)];
        var indexes = columns.Select(c => IndexOf(spec, c.Name)).ToArray();

        // Evenly spaced rows: deterministic, and not biased to the first inserted rows.
        var step = Math.Max(1, data.Count / Math.Max(1, rows));
        IReadOnlyList<object?[]> sample = [.. data
            .Where((_, i) => i % step == 0)
            .Take(rows)
            .Select(row => indexes.Select(i => row[i]).ToArray())];
        return Task.FromResult(sample);
    }

    public async IAsyncEnumerable<object?[]> ReadAllAsync(TableInfo table, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        foreach (var row in _tables[Key(table.Schema, table.Name)].Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return (object?[])row.Clone();
        }

        await Task.CompletedTask;
    }

    public Task CreateTableAsync(string schema, TableSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _tables[Key(schema, spec.Name)] = (spec, []);
        return Task.CompletedTask;
    }

    public Task DropTableIfExistsAsync(string schema, string table, CancellationToken cancellationToken = default)
    {
        _tables.TryRemove(Key(schema, table), out _);
        return Task.CompletedTask;
    }

    public Task CreateEmptyCopyAsync(TableInfo source, string targetSchema, IReadOnlyCollection<string> makeNullable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(makeNullable);
        var spec = _tables[Key(source.Schema, source.Name)].Spec;
        var copy = spec with
        {
            Columns = [.. spec.Columns.Select(c => makeNullable.Contains(c.Name, StringComparer.OrdinalIgnoreCase) ? c with { Nullable = true } : c)],
        };
        _tables[Key(targetSchema, source.Name)] = (copy, []);
        return Task.CompletedTask;
    }

    public async Task<long> InsertAsync(string schema, string table, IReadOnlyList<string> columns, IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        var (spec, data) = _tables[Key(schema, table)];
        var indexes = columns.Select(c => IndexOf(spec, c)).ToArray();
        long count = 0;
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            var stored = new object?[spec.Columns.Count];
            for (var i = 0; i < indexes.Length; i++)
            {
                var column = spec.Columns[indexes[i]];
                if (row[i] is null && !column.Nullable)
                {
                    throw new InvalidOperationException($"NULL into NOT NULL column {table}.{column.Name}.");
                }

                stored[indexes[i]] = row[i];
            }

            lock (data)
            {
                data.Add(stored);
            }

            count++;
        }

        return count;
    }

    public Task GrantSelectAsync(string schema, string table, string grantee, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static string Key(string schema, string table) => schema + "." + table;

    private static int IndexOf(TableSpec spec, string column)
    {
        for (var i = 0; i < spec.Columns.Count; i++)
        {
            if (string.Equals(spec.Columns[i].Name, column, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new ArgumentException($"Unknown column '{column}' in table '{spec.Name}'.", nameof(column));
    }

    private static TableInfo ToInfo(string schema, TableSpec spec, long rows) =>
        new(schema, spec.Name, rows, spec.Comment, [.. spec.Columns.Select((c, i) => new ColumnInfo(
            c.Name,
            c.Type.ToString().ToUpperInvariant(),
            c.Type switch
            {
                LogicalType.Integer64 or LogicalType.Amount => ColumnKind.Number,
                LogicalType.Date or LogicalType.Timestamp => ColumnKind.Temporal,
                LogicalType.Binary => ColumnKind.Binary,
                _ => ColumnKind.Text,
            },
            c.Type is LogicalType.Text ? c.Length : null,
            c.Nullable,
            i + 1,
            c.Comment))]);
}
