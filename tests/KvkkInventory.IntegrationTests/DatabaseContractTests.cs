using System.Data.Common;
using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Evaluation;
using KvkkInventory.Masking;
using KvkkInventory.Scanning;
using KvkkInventory.Schema;
using KvkkInventory.Synthetic;
using KvkkInventory.Validation;

namespace KvkkInventory.IntegrationTests;

/// <summary>
/// The same contract, run against real Oracle and PostgreSQL: least privilege, scan parity with the in-memory
/// reference, accuracy on the labelled schema, and masking into a separate schema.
/// </summary>
public abstract class DatabaseContractTests(IDatabaseFixture db)
{
    internal static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(i => (byte)(i * 7))];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Catalog_lists_every_granted_table_and_hides_ungranted_ones()
    {
        var tables = await db.Scanner.ReadCatalogAsync(db.SourceSchema, Ct);

        tables.Select(t => t.Name.ToUpperInvariant()).Should().BeEquivalentTo(ClinicSchema.Tables().Select(t => t.Name.ToUpperInvariant()));
        tables.Should().NotContain(t => t.Name.Equals("secret_notes", StringComparison.OrdinalIgnoreCase),
            "the scanner sees only what it was granted, so a scan never exceeds its mandate");

        var personnel = tables.Single(t => t.Name.Equals("personel", StringComparison.OrdinalIgnoreCase));
        personnel.Column("parmak_izi_sablonu")!.Kind.Should().Be(ColumnKind.Binary);
        personnel.Column("tckn")!.Kind.Should().Be(ColumnKind.Number);
        personnel.Column("birth_date")!.Kind.Should().Be(ColumnKind.Temporal);
        personnel.Column("ad_soyad")!.MaxLength.Should().Be(100);
        personnel.Comment.Should().Be("Personel özlük bilgileri", "table comments are read and used as context");
    }

    [Fact]
    public async Task Scanner_account_cannot_change_data()
    {
        var act = () => db.ExecuteAsync(db.Scanner, $"DELETE FROM {db.SourceSchema}.hastalar");

        await act.Should().ThrowAsync<DbException>();
    }

    [Fact]
    public async Task Scan_classifies_exactly_like_the_in_memory_reference()
    {
        var classification = Classifier.Classify(await new SchemaScanner(db.Scanner, new DetectionEngine()).ScanAsync(db.SourceSchema, Ct));

        var memory = new InMemoryDatabase();
        await new SyntheticDataLoader(memory).LoadAsync("ref", ClinicSchema.Tables(), FixtureDefaults.Rows, cancellationToken: Ct);
        var reference = Classifier.Classify(await new SchemaScanner(memory, new DetectionEngine()).ScanAsync("ref", Ct));

        Labels(classification).Should().BeEquivalentTo(Labels(reference),
            "an Oracle NUMBER(11) TCKN, a PostgreSQL varchar TCKN and an in-memory string must reach the detectors identically");
    }

    [Fact]
    public async Task Accuracy_on_the_labelled_schema_meets_the_documented_floor()
    {
        var classification = Classifier.Classify(await new SchemaScanner(db.Scanner, new DetectionEngine()).ScanAsync(db.SourceSchema, Ct));
        var result = Evaluator.Evaluate(classification, ClinicSchema.Tables().GroundTruth());
        var report = Evaluator.ToMarkdown(result) + "\n" + string.Join("\n", result.Errors);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, $"accuracy-{db.Dialect}.md"), report, Ct);

        result.Micro.Precision.Should().BeGreaterThan(0.90, report);
        result.Micro.Recall.Should().BeGreaterThan(0.90, report);
    }

    [Fact]
    public async Task Masked_copy_is_complete_valid_irreversible_and_deterministic()
    {
        var scan = await new SchemaScanner(db.Scanner, new DetectionEngine()).ScanAsync(db.SourceSchema, Ct);
        var plan = MaskingPlan.From(Classifier.Classify(scan));
        var runner = new MaskingRunner(db.Masker, new Masker(Key));

        var summary = await runner.RunAsync(db.SourceSchema, db.TargetSchema, plan, Ct);
        summary.Tables.Should().HaveCount(ClinicSchema.Tables().Count);
        summary.Rows.Should().Be(ClinicSchema.Tables().Count * (long)FixtureDefaults.Rows);

        var source = await ReadAsync(db.Masker, db.SourceSchema, "hastalar");
        var masked = await ReadAsync(db.Masker, db.TargetSchema, "hastalar");
        masked.Should().HaveCount(source.Count);

        var sourceTckn = source.Select(r => Text(r["TC_KIMLIK_NO"])).ToHashSet();
        var maskedTckn = masked.Select(r => Text(r["TC_KIMLIK_NO"])).ToList();
        maskedTckn.Should().OnlyContain(t => Checksums.IsValidTckn(t), "format-preserving masking keeps TCKN check digits valid");
        maskedTckn.Should().NotIntersectWith(sourceTckn);
        masked.Select(r => Text(r["EPOSTA"])).Where(e => e.Length > 0).Should().OnlyContain(e => e.EndsWith("@example.invalid", StringComparison.Ordinal));
        masked.Select(r => r["KAN_GRUBU"]).Should().OnlyContain(v => v == null, "special categories are removed from the test copy");

        var personnel = await ReadAsync(db.Masker, db.TargetSchema, "personel");
        personnel.Select(r => Text(r["IBAN"]).Replace(" ", string.Empty, StringComparison.Ordinal)).Should().OnlyContain(i => Checksums.IsValidIban(i));
        personnel.Select(r => Text(r["TCKN"])).Should().OnlyContain(t => Checksums.IsValidTckn(t), "a NUMBER/bigint TCKN stays numeric and valid");
        personnel.Select(r => r["DIN"]).Should().OnlyContain(v => v == null);

        // Run again: same key, same input → byte-identical output (joins across tables and runs keep working).
        await runner.RunAsync(db.SourceSchema, db.TargetSchema, plan, Ct);
        var again = await ReadAsync(db.Masker, db.TargetSchema, "hastalar");
        again.Select(r => Text(r["TC_KIMLIK_NO"])).Should().Equal(maskedTckn);
    }

    [Fact]
    public async Task Masked_schema_still_scans_as_the_same_types_but_holds_none_of_the_values()
    {
        var plan = MaskingPlan.From(Classifier.Classify(await new SchemaScanner(db.Scanner, new DetectionEngine()).ScanAsync(db.SourceSchema, Ct)));
        await new MaskingRunner(db.Masker, new Masker(Key)).RunAsync(db.SourceSchema, db.TargetSchema, plan, Ct);

        var rescan = Classifier.Classify(await new SchemaScanner(db.Masker, new DetectionEngine()).ScanAsync(db.TargetSchema, Ct));
        var labels = Labels(rescan);
        labels.Should().Contain(("HASTALAR", "TC_KIMLIK_NO", nameof(PiiType.NationalId)));
        labels.Should().Contain(("PERSONEL", "IBAN", nameof(PiiType.Iban)));
        labels.Should().Contain(("CUSTOMERS", "EMAIL_ADDRESS", nameof(PiiType.Email)));
    }

    internal static async Task<List<Dictionary<string, object?>>> ReadAsync(IRelationalDatabase database, string schema, string table)
    {
        var info = (await database.ReadCatalogAsync(schema)).Single(t => t.Name.Equals(table, StringComparison.OrdinalIgnoreCase));
        var rows = new List<Dictionary<string, object?>>();
        await foreach (var row in database.ReadAllAsync(info))
        {
            rows.Add(info.Columns.Select((c, i) => (c.Name.ToUpperInvariant(), row[i])).ToDictionary(x => x.Item1, x => x.Item2));
        }

        return [.. rows.OrderBy(r => Convert.ToInt64(r.GetValueOrDefault("ID") ?? 0L, System.Globalization.CultureInfo.InvariantCulture))];
    }

    private static string Text(object? value) => ValueFormatter.ToText(value) ?? string.Empty;

    private static HashSet<(string, string, string)> Labels(ClassificationReport report) =>
        [.. report.Columns.Select(c => (c.Table.ToUpperInvariant(), c.Column.ToUpperInvariant(), c.Type.ToString()))];
}

public sealed class PostgresContractTests(PostgresFixture fixture) : DatabaseContractTests(fixture);

public sealed class OracleContractTests(OracleFixture fixture) : DatabaseContractTests(fixture);
