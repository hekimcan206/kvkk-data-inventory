using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DotNet.Testcontainers.Configurations;
using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Evaluation;
using KvkkInventory.Masking;
using KvkkInventory.Oracle;
using KvkkInventory.Postgres;
using KvkkInventory.Scanning;
using KvkkInventory.Schema;
using KvkkInventory.Synthetic;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;
using Testcontainers.PostgreSql;

// Accuracy + scan-time + masking-throughput benchmark on real Oracle 23ai Free and PostgreSQL 17 containers.
//   dotnet run -c Release --project benchmarks/KvkkInventory.Benchmarks [-- --copies 20 --rows 500 --tall 200000 --runs 3]
// Writes benchmarks/results/latest.md. Every number in docs/benchmarks.md comes from that file.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
var argsMap = args.Select((a, i) => (a, i)).Where(x => x.a.StartsWith("--", StringComparison.Ordinal))
    .ToDictionary(x => x.a[2..], x => x.i + 1 < args.Length ? args[x.i + 1] : "");
int Arg(string name, int fallback) => argsMap.TryGetValue(name, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;
var copies = Arg("copies", 20);
var rows = Arg("rows", 500);
var tallRows = Arg("tall", 200_000);
var runs = Arg("runs", 3);

var root = FindRoot();
var md = new StringBuilder();
md.AppendLine("# Benchmark results (generated)").AppendLine();
md.AppendLine(CultureInfo.InvariantCulture, $"- Date (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
md.AppendLine(CultureInfo.InvariantCulture, $"- CPU: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString()}, {Environment.ProcessorCount} logical cores");
md.AppendLine(CultureInfo.InvariantCulture, $"- RAM visible to .NET: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024 / 1024:F1} GB");
md.AppendLine(CultureInfo.InvariantCulture, $"- OS: {RuntimeInformation.OSDescription}; .NET {Environment.Version}");
md.AppendLine(CultureInfo.InvariantCulture, $"- Docker: {Shell("docker", "version --format {{.Server.Version}}")}; other running containers on the host: {Shell("docker", "ps -q").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length}");
md.AppendLine(CultureInfo.InvariantCulture, $"- Parameters: labelled schema 14 tables × 300 rows; wide schema {copies} copies × 14 tables × {rows} rows; tall table {tallRows:N0} rows; {runs} runs per measurement, median reported").AppendLine();

const string Owner = "OwnerBench1", Scanner = "ScannerBench1", Masker = "MaskerBench1";
var executable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute | UnixFileModes.GroupRead | UnixFileModes.GroupExecute | UnixFileModes.OtherRead | UnixFileModes.OtherExecute;

await using var pg = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("kvkk")
    .WithEnvironment("OWNER_PASSWORD", Owner).WithEnvironment("SCANNER_PASSWORD", Scanner).WithEnvironment("MASKER_PASSWORD", Masker)
    .WithResourceMapping(File.ReadAllBytes(Path.Combine(root, "deploy/postgres/init/01-roles.sh")), "/docker-entrypoint-initdb.d/01-roles.sh", fileMode: executable)
    .WithLabel("project", "kvkk-data-inventory-bench").Build();
await using var ora = new OracleBuilder("gvenzl/oracle-free:23-slim-faststart")
    .WithEnvironment("OWNER_PASSWORD", Owner).WithEnvironment("SCANNER_PASSWORD", Scanner).WithEnvironment("MASKER_PASSWORD", Masker)
    .WithResourceMapping(File.ReadAllBytes(Path.Combine(root, "deploy/oracle/init/01-users.sh")), "/container-entrypoint-initdb.d/01-users.sh", fileMode: executable)
    .WithLabel("project", "kvkk-data-inventory-bench").Build();
Console.Error.WriteLine("starting PostgreSQL and Oracle containers …");
await Task.WhenAll(pg.StartAsync(), ora.StartAsync());

string PgCs(string user, string pw) => new NpgsqlConnectionStringBuilder(pg.GetConnectionString()) { Username = user, Password = pw, Timeout = 60, CommandTimeout = 600 }.ConnectionString;
string OraCs(string user, string pw) => new OracleConnectionStringBuilder(ora.GetConnectionString()) { UserID = user, Password = pw, ConnectionTimeout = 60 }.ConnectionString;

var engines = new[]
{
    new Engine("PostgreSQL 17", new PostgresRelationalDatabase(PgCs("clinic_owner", Owner)), new PostgresRelationalDatabase(PgCs("kvkk_scanner", Scanner)),
        new PostgresRelationalDatabase(PgCs("kvkk_masker", Masker)), "clinic", "clinic_masked", "ANALYZE"),
    new Engine("Oracle 23ai Free", new OracleRelationalDatabase(OraCs("clinic", Owner)), new OracleRelationalDatabase(OraCs("kvkk_scanner", Scanner)),
        new OracleRelationalDatabase(OraCs("kvkk_masked", Masker)), "CLINIC", "KVKK_MASKED", "BEGIN DBMS_STATS.GATHER_SCHEMA_STATS('CLINIC'); END;"),
};

var accuracy = new StringBuilder("## Accuracy on the labelled schema\n\n");
var ablation = new StringBuilder("## Ablation (micro-averaged over all detectors)\n\n| Engine | Mode | Precision | Recall | F1 |\n|---|---|---:|---:|---:|\n");
var scanTimes = new StringBuilder("## Scan time\n\n| Engine | Tables | Columns | Sample rows/table | Median scan (ms) | Columns/s |\n|---|---:|---:|---:|---:|---:|\n");
var sampling = new StringBuilder("## Sampling cost vs table size (one table, 200-row sample)\n\n| Engine | Table rows | Median sample query (ms) |\n|---|---:|---:|\n");
var masking = new StringBuilder("## Masking throughput (copy to masked schema)\n\n| Engine | Tables | Rows | Median duration (ms) | Rows/s |\n|---|---:|---:|---:|---:|\n");

foreach (var e in engines)
{
    Console.Error.WriteLine($"[{e.Name}] loading labelled schema …");
    var labelled = ClinicSchema.Tables();
    await new SyntheticDataLoader(e.Owner).LoadAsync(e.Source, labelled, 300, grantSelectTo: "kvkk_read");

    // Accuracy and ablation: scanned through the read-only account, exactly as in production use.
    foreach (var (mode, detection, classifier) in new (string, DetectionOptions, ClassifierOptions)[]
    {
        ("names + values + linkage (default)", DetectionOptions.Default, new ClassifierOptions()),
        ("names + values, no linkage rule", DetectionOptions.Default, new ClassifierOptions { RequireSubjectLink = false }),
        ("column names only", new DetectionOptions { UseValues = false }, new ClassifierOptions()),
        ("sampled values only", new DetectionOptions { UseNames = false }, new ClassifierOptions()),
    })
    {
        var classification = Classifier.Classify(await new SchemaScanner(e.Scanner, new DetectionEngine(detection)).ScanAsync(e.Source), classifier);
        var result = Evaluator.Evaluate(classification, labelled.GroundTruth());
        ablation.AppendLine(CultureInfo.InvariantCulture, $"| {e.Name} | {mode} | {result.Micro.Precision:F3} | {result.Micro.Recall:F3} | {result.Micro.F1:F3} |");
        if (detection == DetectionOptions.Default && classifier.RequireSubjectLink)
        {
            accuracy.AppendLine(CultureInfo.InvariantCulture, $"### {e.Name}\n").Append(Evaluator.ToMarkdown(result)).AppendLine()
                .AppendLine("Errors:\n").AppendLine("```").AppendLine(string.Join("\n", result.Errors)).AppendLine("```").AppendLine();
        }
    }

    Console.Error.WriteLine($"[{e.Name}] loading wide schema ({copies} copies) …");
    var wide = Enumerable.Range(1, copies).SelectMany(i => labelled.Select(t => t.WithSuffix(string.Create(CultureInfo.InvariantCulture, $"_{i:000}")))).ToArray();
    var load = await new SyntheticDataLoader(e.Owner).LoadAsync(e.Source, wide, rows, grantSelectTo: "kvkk_read");
    Console.Error.WriteLine($"[{e.Name}] loaded {load.Rows:N0} rows in {load.DurationMs:F0} ms");
    await e.ExecuteAsOwner(e.AnalyzeSql);

    foreach (var sample in new[] { 100, 200, 1000 })
    {
        var timings = new List<double>();
        ScanReport? last = null;
        for (var run = 0; run < runs + 1; run++)
        {
            last = await new SchemaScanner(e.Scanner, new DetectionEngine(), new ScanOptions { SampleSize = sample, Parallelism = 4 }).ScanAsync(e.Source);
            if (run > 0)
            {
                timings.Add(last.DurationMs); // run 0 warms connection pools and JIT
            }
        }

        var median = Median(timings);
        scanTimes.AppendLine(CultureInfo.InvariantCulture,
            $"| {e.Name} | {last!.Tables.Count} | {last.ColumnCount} | {sample} | {median:F0} | {last.ColumnCount / (median / 1000):F0} |");
    }

    Console.Error.WriteLine($"[{e.Name}] loading tall table ({tallRows:N0} rows) …");
    var tall = labelled[0].WithSuffix("_tall");
    await new SyntheticDataLoader(e.Owner).LoadAsync(e.Source, [tall], tallRows, grantSelectTo: "kvkk_read");
    await e.ExecuteAsOwner(e.AnalyzeSql);
    var catalog = await e.Scanner.ReadCatalogAsync(e.Source);
    foreach (var name in new[] { labelled[0].Name, tall.Name })
    {
        var table = catalog.Single(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var columns = table.Columns.Where(c => c.IsSampleable).ToArray();
        var timings = new List<double>();
        for (var run = 0; run < runs + 2; run++)
        {
            var watch = Stopwatch.StartNew();
            await e.Scanner.SampleAsync(table, columns, 200);
            if (run > 1)
            {
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        sampling.AppendLine(CultureInfo.InvariantCulture, $"| {e.Name} | {table.EstimatedRows:N0} | {Median(timings):F1} |");
    }

    Console.Error.WriteLine($"[{e.Name}] masking …");
    var plan = MaskingPlan.From(Classifier.Classify(await new SchemaScanner(e.Scanner, new DetectionEngine()).ScanAsync(e.Source)));
    var maskTimings = new List<double>();
    MaskingSummary? summary = null;
    for (var run = 0; run < runs; run++)
    {
        summary = await new MaskingRunner(e.Masker, new Masker([.. Enumerable.Range(0, 32).Select(i => (byte)i)])).RunAsync(e.Source, e.Target, plan);
        maskTimings.Add(summary.DurationMs);
    }

    var maskMedian = Median(maskTimings);
    masking.AppendLine(CultureInfo.InvariantCulture, $"| {e.Name} | {summary!.Tables.Count} | {summary.Rows:N0} | {maskMedian:F0} | {summary.Rows / (maskMedian / 1000):N0} |");
}

md.Append(accuracy).AppendLine().Append(ablation).AppendLine().Append(scanTimes).AppendLine().Append(sampling).AppendLine().Append(masking);
var outFile = Path.Combine(root, "benchmarks", "results", "latest.md");
Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
await File.WriteAllTextAsync(outFile, md.ToString().ReplaceLineEndings("\n"));
Console.WriteLine(md);
return 0;

static double Median(List<double> values)
{
    var sorted = values.Order().ToArray();
    return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
}

static string FindRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KvkkInventory.slnx")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? throw new InvalidOperationException("Run from inside the repository.");
}

static string Shell(string file, string arguments)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments) { RedirectStandardOutput = true, UseShellExecute = false })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return output;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return "n/a";
    }
}

internal sealed record Engine(string Name, IRelationalDatabase Owner, IRelationalDatabase Scanner, IRelationalDatabase Masker, string Source, string Target, string AnalyzeSql)
{
    public Task ExecuteAsOwner(string sql) => Owner switch
    {
        PostgresRelationalDatabase p => p.ExecuteAsync(sql),
        OracleRelationalDatabase o => o.ExecuteAsync(sql),
        _ => Task.CompletedTask,
    };
}
