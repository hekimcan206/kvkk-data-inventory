using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Evaluation;
using KvkkInventory.Inventory;
using KvkkInventory.Scanning;
using KvkkInventory.Schema;
using KvkkInventory.Synthetic;

namespace KvkkInventory.UnitTests.Evaluation;

/// <summary>
/// Accuracy on the labelled synthetic schema without a database. The committed numbers in docs/benchmarks.md come
/// from the Oracle and PostgreSQL scans (benchmarks project); this test guards against regressions on every build.
/// </summary>
public sealed class InMemoryAccuracyTests
{
    internal static async Task<(ClassificationReport Classification, EvaluationResult Result)> RunAsync(
        DetectionOptions? detection = null, ClassifierOptions? classifier = null, int rows = 300)
    {
        var db = new InMemoryDatabase();
        var tables = ClinicSchema.Tables();
        await new SyntheticDataLoader(db).LoadAsync("clinic", tables, rows, cancellationToken: TestContext.Current.CancellationToken);
        var scan = await new SchemaScanner(db, new DetectionEngine(detection)).ScanAsync("clinic", TestContext.Current.CancellationToken);
        var classification = Classifier.Classify(scan, classifier);
        return (classification, Evaluator.Evaluate(classification, tables.GroundTruth()));
    }

    [Fact]
    public async Task Combined_detection_reaches_the_documented_accuracy()
    {
        var (_, result) = await RunAsync();
        var report = Evaluator.ToMarkdown(result) + "\n" + string.Join("\n", result.Errors);

        result.Micro.Precision.Should().BeGreaterThan(0.90, report);
        result.Micro.Recall.Should().BeGreaterThan(0.90, report);
        // The remaining errors are the documented limitations, not noise: pin them so any change is noticed.
        result.Errors.Should().BeEquivalentTo(
        [
            "FN Health                 randevular.bolum_adi",
            "FP Health                 randevular.oda_no",
            "FN Health                 receteler.ilac_adi",
            "FP Phone                  subeler.telefon",
            "FP Address                subeler.adres",
            "FN Genetic                genetik_testler.sonuc",
        ], report);
    }

    [Fact]
    public async Task Names_and_values_together_beat_either_alone()
    {
        var (_, combined) = await RunAsync();
        var (_, namesOnly) = await RunAsync(new DetectionOptions { UseValues = false });
        var (_, valuesOnly) = await RunAsync(new DetectionOptions { UseNames = false });

        combined.Micro.F1.Should().BeGreaterThan(namesOnly.Micro.F1);
        combined.Micro.F1.Should().BeGreaterThan(valuesOnly.Micro.F1);
    }

    [Fact]
    public async Task Reference_tables_do_not_count_as_health_data()
    {
        var (classification, _) = await RunAsync();
        classification.Columns.Should().NotContain(c => c.Table == "icd_kodlari");
        classification.Suppressed.Should().Contain(s => s.Table == "icd_kodlari" && s.Type == PiiType.Health);

        var (withoutLinkage, result) = await RunAsync(classifier: new ClassifierOptions { RequireSubjectLink = false });
        withoutLinkage.Columns.Should().Contain(c => c.Table == "icd_kodlari" && c.Type == PiiType.Health);
        result.Errors.Should().Contain(e => e.Contains("icd_kodlari", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_output_contains_no_sampled_values()
    {
        var db = new InMemoryDatabase();
        var tables = ClinicSchema.Tables();
        await new SyntheticDataLoader(db).LoadAsync("clinic", tables, 50, cancellationToken: TestContext.Current.CancellationToken);
        var scan = await new SchemaScanner(db, new DetectionEngine()).ScanAsync("clinic", TestContext.Current.CancellationToken);
        var json = System.Text.Json.JsonSerializer.Serialize(scan, KvkkJson.Options);

        var tckns = SyntheticDataLoader.Generate(tables[0], 50, 20261107).Select(r => (string)r[1]!).ToArray();
        tckns.Should().HaveCount(50);
        json.Should().NotContainAny(tckns);
    }
}
