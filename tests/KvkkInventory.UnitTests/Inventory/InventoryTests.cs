using KvkkInventory.Classification;
using KvkkInventory.Inventory;
using KvkkInventory.Masking;

namespace KvkkInventory.UnitTests.Inventory;

public sealed class InventoryTests
{
    private static ClassifiedColumn Col(string table, string column, PiiType type)
    {
        var category = KvkkTaxonomy.CategoryOf(type);
        return new(table, column, type, category.Code, category.Name, category.Special, 0.9, SubjectLinkage.Direct, MaskingDefaults.For(type), "test");
    }

    private static readonly ClassificationReport Classification = new("memory", "clinic", DateTimeOffset.UnixEpoch, 0.5, 3, 10,
    [
        Col("hastalar", "tc_kimlik_no", PiiType.NationalId),
        Col("hastalar", "ad", PiiType.PersonName),
        Col("hastalar", "kan_grubu", PiiType.Health),
        Col("personel", "tckn", PiiType.NationalId),
        Col("personel", "maas", PiiType.Salary),
        Col("orphan", "eposta", PiiType.Email),
    ], []);

    private static readonly InventoryPolicy Policy = new()
    {
        Controller = "Örnek Klinik A.Ş. (synthetic)",
        SubjectGroups = [new("Hasta", ["hastalar"]), new("Çalışan", ["personel"])],
        Processing = new Dictionary<string, ProcessingDetails>
        {
            ["Hasta:identity"] = new() { Purposes = ["Sağlık hizmetlerinin yürütülmesi"], Retention = "20 yıl", LegalBases = ["KVKK m.5/2-ç"] },
            ["identity"] = new() { Purposes = ["Genel"], Retention = "10 yıl" },
            ["health"] = new() { Purposes = ["Tıbbi teşhis ve tedavi"], Retention = "20 yıl", Recipients = ["Yetkili kamu kurum ve kuruluşları"] },
        },
    };

    [Fact]
    public void Entries_are_grouped_by_subject_group_and_category_with_group_specific_policy_first()
    {
        var inventory = InventoryBuilder.Build(Classification, Policy);

        var patientIdentity = inventory.Entries.Single(e => e.SubjectGroup == "Hasta" && e.CategoryCode == "identity");
        patientIdentity.Purposes.Should().Equal("Sağlık hizmetlerinin yürütülmesi");
        patientIdentity.Sources.Should().Equal("hastalar.ad (PersonName)", "hastalar.tc_kimlik_no (NationalId)");

        inventory.Entries.Single(e => e.SubjectGroup == "Çalışan" && e.CategoryCode == "identity").Purposes.Should().Equal("Genel");
        inventory.Entries.Single(e => e.CategoryCode == "health").Special.Should().BeTrue();
    }

    [Fact]
    public void Categories_without_a_documented_purpose_and_unassigned_tables_are_reported_as_gaps()
    {
        var inventory = InventoryBuilder.Build(Classification, Policy);

        inventory.Entries.Single(e => e.CategoryCode == "personnel").PolicyMissing.Should().BeTrue();
        inventory.Gaps.Should().Contain(g => g.Contains("Özlük", StringComparison.Ordinal));
        inventory.Gaps.Should().Contain(g => g.Contains("orphan", StringComparison.Ordinal));
        inventory.Entries.Single(e => e.CategoryCode == "contact").SubjectGroup.Should().Be("Diğer");
    }

    [Fact]
    public void Renderers_produce_csv_and_markdown_with_turkish_headers()
    {
        var inventory = InventoryBuilder.Build(Classification, Policy);

        var csv = InventoryRenderer.Csv(inventory);
        csv.Split('\n')[0].Should().StartWith("veri_konusu_kisi_grubu,veri_kategorisi,ozel_nitelikli");
        csv.Should().Contain("Sağlık Bilgileri,evet");

        var md = InventoryRenderer.Markdown(inventory);
        md.Should().Contain("| Hasta | Sağlık Bilgileri | **yes** |").And.Contain("## Gaps");
        InventoryRenderer.Json(inventory).Should().Contain("\"category\": \"Kimlik\"");
    }
}
