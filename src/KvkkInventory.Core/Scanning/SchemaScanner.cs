using System.Collections.Concurrent;
using System.Diagnostics;
using KvkkInventory.Detection;
using KvkkInventory.Schema;

namespace KvkkInventory.Scanning;

public sealed record ScanOptions
{
    /// <summary>Rows sampled per table. 200 keeps a scan cheap while giving each value detector enough evidence.</summary>
    public int SampleSize { get; init; } = 200;

    /// <summary>Tables scanned concurrently (each holds one pooled connection).</summary>
    public int Parallelism { get; init; } = 4;
}

/// <summary>Detector evidence for one column. Contains no sampled values.</summary>
public sealed record ColumnScan(string Name, string DataType, int? MaxLength, bool Nullable, string? Comment, IReadOnlyList<Evidence> Evidence);

public sealed record TableScan(string Name, long? EstimatedRows, int SampledRows, IReadOnlyList<ColumnScan> Columns);

/// <summary>Output of <c>kvkk scan</c>: catalog + evidence. Safe to store and share; it is metadata, not data.</summary>
public sealed record ScanReport(
    string Dialect,
    string Schema,
    DateTimeOffset ScannedAt,
    double DurationMs,
    int SampleSize,
    IReadOnlyList<TableScan> Tables)
{
    public int ColumnCount => Tables.Sum(t => t.Columns.Count);
}

/// <summary>
/// Reads the catalog, samples each table once (all sampleable columns in one query) and runs the detection
/// engine on every column. Sampled values live only for the duration of one table's detection.
/// </summary>
public sealed class SchemaScanner(IRelationalDatabase database, DetectionEngine engine, ScanOptions? options = null, TimeProvider? time = null)
{
    private readonly ScanOptions _options = options ?? new ScanOptions();
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public static readonly ActivitySource ActivitySource = new("KvkkInventory.Scan");

    public async Task<ScanReport> ScanAsync(string schema, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        using var activity = ActivitySource.StartActivity("scan");
        activity?.SetTag("db.system", database.Dialect);
        activity?.SetTag("db.schema", schema);

        var started = _time.GetTimestamp();
        var scannedAt = _time.GetUtcNow();
        var tables = await database.ReadCatalogAsync(schema, cancellationToken);
        var results = new ConcurrentDictionary<string, TableScan>(StringComparer.Ordinal);

        await Parallel.ForEachAsync(
            tables,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.Parallelism), CancellationToken = cancellationToken },
            async (table, ct) => results[table.Name] = await ScanTableAsync(table, ct));

        var ordered = tables.Select(t => results[t.Name]).ToArray();
        var elapsed = _time.GetElapsedTime(started);
        activity?.SetTag("kvkk.tables", ordered.Length);
        return new ScanReport(database.Dialect, schema, scannedAt, Math.Round(elapsed.TotalMilliseconds, 1), _options.SampleSize, ordered);
    }

    private async Task<TableScan> ScanTableAsync(TableInfo table, CancellationToken cancellationToken)
    {
        var sampleable = table.Columns.Where(c => c.IsSampleable).ToArray();
        var rows = sampleable.Length == 0
            ? []
            : await database.SampleAsync(table, sampleable, _options.SampleSize, cancellationToken);

        var columns = new List<ColumnScan>(table.Columns.Count);
        foreach (var column in table.Columns)
        {
            var index = Array.IndexOf(sampleable, column);
            IReadOnlyList<string> samples = index < 0
                ? []
                : [.. rows.Select(r => ValueFormatter.ToText(r[index])).OfType<string>().Where(v => !string.IsNullOrWhiteSpace(v))];

            var context = new ColumnContext(table.Name, column.Name, column.DataType, column.Comment, samples,
                IsBinary: column.Kind is ColumnKind.Binary or ColumnKind.LargeText);
            columns.Add(new ColumnScan(column.Name, column.DataType, column.MaxLength, column.Nullable, column.Comment, engine.Detect(context)));
        }

        return new TableScan(table.Name, table.EstimatedRows, rows.Count, columns);
    }
}
