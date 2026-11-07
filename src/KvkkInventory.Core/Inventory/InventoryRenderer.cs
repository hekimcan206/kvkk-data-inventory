using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KvkkInventory.Inventory;

/// <summary>Renders the inventory as JSON (machine), CSV (spreadsheet, one row per VERBİS entry) and Markdown (review).</summary>
public static class InventoryRenderer
{
    public static string Json(VerbisInventory inventory) =>
        JsonSerializer.Serialize(inventory, KvkkJson.Options).ReplaceLineEndings("\n") + "\n";

    public static string Csv(VerbisInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var builder = new StringBuilder();
        builder.Append("veri_konusu_kisi_grubu,veri_kategorisi,ozel_nitelikli,isleme_amaclari,hukuki_sebepler,azami_saklama_suresi,alici_gruplari,yurt_disi_aktarim,kaynak_kolonlar\n");
        foreach (var e in inventory.Entries)
        {
            builder.Append(Field(e.SubjectGroup)).Append(',')
                .Append(Field(e.Category)).Append(',')
                .Append(e.Special ? "evet" : "hayır").Append(',')
                .Append(Field(string.Join("; ", e.Purposes))).Append(',')
                .Append(Field(string.Join("; ", e.LegalBases))).Append(',')
                .Append(Field(e.Retention)).Append(',')
                .Append(Field(string.Join("; ", e.Recipients))).Append(',')
                .Append(Field(e.ForeignTransfer.Planned ? "evet: " + string.Join("; ", e.ForeignTransfer.Countries) : "hayır")).Append(',')
                .Append(Field(string.Join("; ", e.Sources))).Append('\n');
        }

        return builder.ToString();
    }

    public static string Markdown(VerbisInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var md = new StringBuilder();
        md.Append("# Personal data inventory (VERBİS-style)\n\n");
        md.Append(CultureInfo.InvariantCulture, $"- Data controller: **{inventory.Controller}**\n");
        md.Append(CultureInfo.InvariantCulture, $"- Source: `{inventory.Dialect}` schema `{inventory.Schema}`\n");
        md.Append(CultureInfo.InvariantCulture, $"- Generated: {inventory.GeneratedAt:yyyy-MM-dd HH:mm} UTC\n");
        md.Append(CultureInfo.InvariantCulture, $"- Entries: {inventory.Entries.Count} ({inventory.Entries.Count(e => e.Special)} special-category)\n\n");

        md.Append("| Data subject group | Data category | Special | Purposes | Legal basis | Max. retention | Recipient groups | Transfer abroad | Found in |\n");
        md.Append("|---|---|---|---|---|---|---|---|---|\n");
        foreach (var e in inventory.Entries)
        {
            md.Append(CultureInfo.InvariantCulture,
                $"| {Cell(e.SubjectGroup)} | {Cell(e.Category)} | {(e.Special ? "**yes**" : "no")} | {Cell(Join(e.Purposes))} | {Cell(Join(e.LegalBases))} | {Cell(Fallback(e.Retention))} | {Cell(Join(e.Recipients))} | {(e.ForeignTransfer.Planned ? Cell("yes: " + string.Join(", ", e.ForeignTransfer.Countries)) : "no")} | {Cell(string.Join("<br>", e.Sources))} |\n");
        }

        md.Append("\n## Gaps\n\n");
        if (inventory.Gaps.Count == 0)
        {
            md.Append("None: every category found in the database has a documented purpose, legal basis and retention period.\n");
        }
        else
        {
            foreach (var gap in inventory.Gaps)
            {
                md.Append("- ").Append(gap).Append('\n');
            }
        }

        return md.ToString();
    }

    private static string Join(IReadOnlyList<string> values) => values.Count == 0 ? "**missing**" : string.Join("; ", values);

    private static string Fallback(string value) => string.IsNullOrWhiteSpace(value) ? "**missing**" : value;

    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', ';']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
}
