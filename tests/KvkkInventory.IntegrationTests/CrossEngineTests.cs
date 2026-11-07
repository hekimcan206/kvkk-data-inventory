using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Masking;
using KvkkInventory.Scanning;
using KvkkInventory.Schema;

namespace KvkkInventory.IntegrationTests;

/// <summary>
/// The same person in an Oracle system and a PostgreSQL system masks to the same pseudonym under the same key,
/// so masked extracts from two engines can still be joined.
/// </summary>
public sealed class CrossEngineTests(PostgresFixture postgres, OracleFixture oracle)
{
    [Fact]
    public async Task Same_key_masks_the_same_patient_identically_on_both_engines()
    {
        foreach (IDatabaseFixture db in new IDatabaseFixture[] { postgres, oracle })
        {
            var scan = await new SchemaScanner(db.Scanner, new DetectionEngine()).ScanAsync(db.SourceSchema, TestContext.Current.CancellationToken);
            await new MaskingRunner(db.Masker, new Masker(DatabaseContractTests.Key))
                .RunAsync(db.SourceSchema, db.TargetSchema, MaskingPlan.From(Classifier.Classify(scan)), TestContext.Current.CancellationToken);
        }

        var pg = await DatabaseContractTests.ReadAsync(postgres.Masker, postgres.TargetSchema, "hastalar");
        var ora = await DatabaseContractTests.ReadAsync(oracle.Masker, oracle.TargetSchema, "hastalar");

        foreach (var column in new[] { "TC_KIMLIK_NO", "CEP_TELEFONU", "EPOSTA", "AD", "SOYAD" })
        {
            ora.Select(r => ValueFormatter.ToText(r[column])).Should().Equal(pg.Select(r => ValueFormatter.ToText(r[column])), column);
        }

        // Generalised birth dates agree too: an Oracle DATE and a PostgreSQL date both become 1 January.
        ora.Select(r => ValueFormatter.ToText(r["DOGUM_TARIHI"])).Should().Equal(pg.Select(r => ValueFormatter.ToText(r["DOGUM_TARIHI"])));
    }
}
