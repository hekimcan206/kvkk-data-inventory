using System.Globalization;
using System.Text;
using System.Text.Json;
using KvkkInventory.Classification;
using KvkkInventory.Cli;
using KvkkInventory.Detection;
using KvkkInventory.Evaluation;
using KvkkInventory.Inventory;
using KvkkInventory.Masking;
using KvkkInventory.Scanning;
using KvkkInventory.Schema;
using KvkkInventory.Synthetic;

// kvkk <command> [options]. Exit codes: 0 ok, 1 usage/input error, 2 inventory has gaps (export) — so `kvkk export`
// can gate a release pipeline on "every personal-data category has a documented purpose and retention".
Console.OutputEncoding = Encoding.UTF8;

try
{
    var options = Options.Parse(args.Length > 1 ? args[1..] : []);
    return args.FirstOrDefault() switch
    {
        "synth" => await SynthAsync(options),
        "scan" => await ScanAsync(options),
        "classify" => Classify(options),
        "mask" => await MaskAsync(options),
        "export" => Export(options),
        "evaluate" => Evaluate(options),
        "demo" => await DemoAsync(options),
        "pipeline" => await PipelineAsync(options),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is IOException or JsonException or FormatException or ArgumentException or InvalidOperationException
    or System.Data.Common.DbException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static async Task<int> SynthAsync(Options o)
{
    var db = Databases.Open(o);
    var schema = o.Require("schema");
    var rows = o.Int("rows", 500);
    var copies = o.Int("copies", 1);
    var tables = ClinicSchema.Tables();
    // --copies N replicates the labelled schema N times (suffix _001 …) for scan-time benchmarks on wide schemas.
    IReadOnlyList<SyntheticTable> all = copies <= 1
        ? tables
        : [.. Enumerable.Range(1, copies).SelectMany(i => tables.Select(t => t.WithSuffix(string.Create(CultureInfo.InvariantCulture, $"_{i:000}"))))];

    var result = await new SyntheticDataLoader(db).LoadAsync(schema, all, rows, o.Int("seed", 20261107), o.Get("grant"));
    if (o.Get("labels") is { } labels)
    {
        await File.WriteAllTextAsync(labels, JsonSerializer.Serialize(all.GroundTruth(), KvkkJson.Options));
    }

    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"loaded {result.Rows} synthetic rows into {result.Tables} tables of {db.Dialect} schema '{schema}' in {result.DurationMs:F0} ms"));
    return 0;
}

static async Task<int> ScanAsync(Options o)
{
    var db = Databases.Open(o);
    var engine = new DetectionEngine(new DetectionOptions { UseNames = !o.Has("values-only"), UseValues = !o.Has("names-only") });
    var scanner = new SchemaScanner(db, engine, new ScanOptions { SampleSize = o.Int("sample", 200), Parallelism = o.Int("parallel", 4) });
    var report = await scanner.ScanAsync(o.Require("schema"));
    Write(o.Get("out"), JsonSerializer.Serialize(report, KvkkJson.Options));
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"scanned {report.Tables.Count} tables / {report.ColumnCount} columns of {report.Dialect} schema '{report.Schema}' in {report.DurationMs:F0} ms (sample {report.SampleSize} rows/table)"));
    return 0;
}

static int Classify(Options o)
{
    var scan = Read<ScanReport>(o.Require("in"));
    var classification = Classifier.Classify(scan, new ClassifierOptions { Threshold = o.Double("threshold", 0.5) });
    Write(o.Get("out"), JsonSerializer.Serialize(classification, KvkkJson.Options));
    Console.Error.WriteLine($"{classification.Columns.Count} personal-data findings in {classification.Columns.Select(c => (c.Table, c.Column)).Distinct().Count()} columns "
        + $"({classification.Columns.Count(c => c.Special)} special-category); {classification.Suppressed.Count} suppressed as reference data");
    foreach (var c in classification.Columns.OrderBy(c => c.Table, StringComparer.Ordinal).ThenBy(c => c.Column, StringComparer.Ordinal))
    {
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {c.Table + "." + c.Column,-38} {c.Type,-22} {c.CategoryName,-28} {c.Confidence:F2}  → {c.Masking}"));
    }

    return 0;
}

static async Task<int> MaskAsync(Options o)
{
    var db = Databases.Open(o);
    var keyVariable = o.Get("key-env") ?? "KVKK_MASKING_KEY";
    var key = Environment.GetEnvironmentVariable(keyVariable)
        ?? throw new ArgumentException($"Set {keyVariable} to a base64 key of at least 32 bytes (e.g. `openssl rand -base64 32`). The key is never read from the command line.");
    var classification = Read<ClassificationReport>(o.Require("classification"));
    var overrides = o.Get("overrides") is { } path ? Read<Dictionary<string, MaskingStrategy>>(path) : null;
    var plan = MaskingPlan.From(classification, overrides);

    var summary = await new MaskingRunner(db, Masker.FromBase64(key)).RunAsync(o.Require("source"), o.Require("target"), plan);
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"masked copy {summary.SourceSchema} → {summary.TargetSchema}: {summary.Tables.Count} tables, {summary.Rows} rows, "
        + $"{summary.Tables.Sum(t => t.MaskedColumns)} masked columns in {summary.DurationMs:F0} ms"));
    return 0;
}

static int Export(Options o)
{
    var classification = Read<ClassificationReport>(o.Require("classification"));
    var inventory = InventoryBuilder.Build(classification, InventoryPolicy.Load(o.Require("policy")));
    WriteInventory(inventory, o.Require("out"));
    return inventory.Gaps.Count == 0 ? 0 : 2;
}

static int Evaluate(Options o)
{
    var classification = Read<ClassificationReport>(o.Require("classification"));
    var truth = o.Get("labels") is { } labels ? Read<List<LabelledColumn>>(labels) : (IReadOnlyList<LabelledColumn>)ClinicSchema.Tables().GroundTruth();
    var result = Evaluator.Evaluate(classification, truth);
    Write(o.Get("out"), Evaluator.ToMarkdown(result));
    Console.Error.WriteLine(string.Join(Environment.NewLine, result.Errors));
    return 0;
}

// Runs the whole pipeline in memory (no database) and writes the committed samples.
static async Task<int> DemoAsync(Options o)
{
    var outDir = o.Get("out") ?? "docs/samples";
    Directory.CreateDirectory(outDir);
    var db = new InMemoryDatabase();
    await new SyntheticDataLoader(db).LoadAsync("clinic", ClinicSchema.Tables(), o.Int("rows", 300));
    var scan = await new SchemaScanner(db, new DetectionEngine()).ScanAsync("clinic");
    var classification = Classifier.Classify(scan);
    var inventory = InventoryBuilder.Build(classification, InventoryPolicy.Load(o.Get("policy") ?? "samples/inventory-policy.json"));

    Write(Path.Combine(outDir, "classification.json"), JsonSerializer.Serialize(classification, KvkkJson.Options));
    WriteInventory(inventory, outDir);

    // A before/after view of masking on a few synthetic rows, using a published demo key (never use it for real data).
    var masker = new Masker(Convert.FromBase64String(Databases.DemoKey));
    var plan = MaskingPlan.From(classification);
    var patients = (await db.ReadCatalogAsync("clinic")).Single(t => t.Name == "hastalar");
    var mask = plan.For("hastalar")!;
    var md = new StringBuilder("# Masking sample (synthetic data, demo key)\n\n| column | strategy | original | masked |\n|---|---|---|---|\n");
    var rows = new List<object?[]>();
    await foreach (var row in db.ReadAllAsync(patients))
    {
        rows.Add(row);
        if (rows.Count == 2)
        {
            break;
        }
    }

    foreach (var row in rows)
    {
        foreach (var column in mask.Columns)
        {
            var index = patients.Columns.ToList().FindIndex(c => c.Name == column.Column);
            md.Append(CultureInfo.InvariantCulture,
                $"| {column.Column} | {column.Strategy} | {ValueFormatter.ToText(row[index])} | {ValueFormatter.ToText(masker.Mask(row[index], column.Type, column.Strategy)) ?? "NULL"} |\n");
        }
    }

    Write(Path.Combine(outDir, "masking-sample.md"), md.ToString());
    Console.Error.WriteLine($"wrote samples to {outDir}");
    return 0;
}

// The docker compose demo in one process (the chiseled image has no shell to chain commands): seed as the owner,
// scan + classify as the read-only scanner, export, evaluate, then mask as the masking account. Each step uses its
// own account, so the demo exercises the least-privilege split rather than a superuser.
static async Task<int> PipelineAsync(Options o)
{
    var dialect = o.Require("db");
    var schema = o.Require("schema");
    var outDir = o.Require("out");
    Directory.CreateDirectory(outDir);

    var owner = Databases.Open(dialect, "KVKK_OWNER_CONNECTION");
    var scanner = Databases.Open(dialect, "KVKK_SCANNER_CONNECTION");
    var maskerDb = Databases.Open(dialect, "KVKK_MASKER_CONNECTION");

    var loaded = await new SyntheticDataLoader(owner).LoadAsync(schema, ClinicSchema.Tables(), o.Int("rows", 500), grantSelectTo: "kvkk_read");
    Console.Error.WriteLine($"[{dialect}] seeded {loaded.Rows} synthetic rows into {loaded.Tables} tables");

    var scan = await new SchemaScanner(scanner, new DetectionEngine()).ScanAsync(schema);
    var classification = Classifier.Classify(scan);
    Write(Path.Combine(outDir, "scan.json"), JsonSerializer.Serialize(scan, KvkkJson.Options));
    Write(Path.Combine(outDir, "classification.json"), JsonSerializer.Serialize(classification, KvkkJson.Options));
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"[{dialect}] scanned {scan.Tables.Count} tables / {scan.ColumnCount} columns in {scan.DurationMs:F0} ms: {classification.Columns.Count} findings"));

    var result = Evaluator.Evaluate(classification, ClinicSchema.Tables().GroundTruth());
    Write(Path.Combine(outDir, "accuracy.md"), Evaluator.ToMarkdown(result));
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"[{dialect}] accuracy vs ground truth: precision {result.Micro.Precision:F3}, recall {result.Micro.Recall:F3}, F1 {result.Micro.F1:F3}"));

    WriteInventory(InventoryBuilder.Build(classification, InventoryPolicy.Load(o.Get("policy") ?? "samples/inventory-policy.json")), outDir);

    var key = Environment.GetEnvironmentVariable("KVKK_MASKING_KEY") ?? throw new ArgumentException("Set KVKK_MASKING_KEY (base64, ≥ 32 bytes).");
    var summary = await new MaskingRunner(maskerDb, Masker.FromBase64(key)).RunAsync(schema, o.Require("target"), MaskingPlan.From(classification));
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"[{dialect}] masked copy {summary.SourceSchema} → {summary.TargetSchema}: {summary.Rows} rows in {summary.DurationMs:F0} ms"));
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"PIPELINE_OK {dialect} f1={result.Micro.F1:F3} masked_rows={summary.Rows}"));
    return 0;
}

static void WriteInventory(VerbisInventory inventory, string outDir)
{
    Directory.CreateDirectory(outDir);
    Write(Path.Combine(outDir, "verbis-inventory.json"), InventoryRenderer.Json(inventory));
    // UTF-8 with BOM so Excel opens the Turkish headers and values correctly.
    File.WriteAllText(Path.Combine(outDir, "verbis-inventory.csv"), InventoryRenderer.Csv(inventory), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    Write(Path.Combine(outDir, "verbis-inventory.md"), InventoryRenderer.Markdown(inventory));
    Console.Error.WriteLine($"inventory: {inventory.Entries.Count} entries, {inventory.Gaps.Count} gaps → {outDir}");
    foreach (var gap in inventory.Gaps)
    {
        Console.Error.WriteLine($"  gap: {gap}");
    }
}

static T Read<T>(string path)
{
    using var stream = File.OpenRead(path);
    return JsonSerializer.Deserialize<T>(stream, KvkkJson.Options) ?? throw new FormatException($"'{path}' is empty.");
}

static void Write(string? path, string content)
{
    if (path is null)
    {
        Console.Out.Write(content);
        return;
    }

    File.WriteAllText(path, content.ReplaceLineEndings("\n"));
}

static int Usage()
{
    Console.Error.WriteLine("""
        kvkk — find, classify and mask personal data in Oracle / PostgreSQL; export a VERBİS-style inventory

          kvkk synth    --db postgres|oracle --schema S [--rows 500] [--copies 1] [--seed N] [--grant ROLE] [--labels F]
          kvkk scan     --db postgres|oracle --schema S [--sample 200] [--parallel 4] [--names-only|--values-only] [--out scan.json]
          kvkk classify --in scan.json [--threshold 0.5] [--out classification.json]
          kvkk mask     --db postgres|oracle --source S --target T --classification F [--overrides F] [--key-env KVKK_MASKING_KEY]
          kvkk export   --classification F --policy inventory-policy.json --out DIR
          kvkk evaluate --classification F [--labels F] [--out F]
          kvkk demo     [--out docs/samples]           in-memory end-to-end run, writes the sample files
          kvkk pipeline --db postgres|oracle --schema S --target T --out DIR   seed → scan → classify → export → mask (compose demo)

        Connection string: --conn-env NAME (default KVKK_CONNECTION). Passwords never go on the command line.
        """);
    return 1;
}
