using System.Text.Json;
using System.Text.Json.Serialization;

namespace KvkkInventory.Inventory;

/// <summary>
/// What a database cannot tell: why the data is processed, on which legal basis, for how long, and who receives
/// it. The data controller writes this once (see <c>samples/inventory-policy.json</c>); the scan supplies which
/// categories actually exist and where.
/// </summary>
public sealed record InventoryPolicy
{
    public required string Controller { get; init; }

    /// <summary>Data subject groups ("veri konusu kişi grubu") and the tables whose rows are about them.</summary>
    public IReadOnlyList<SubjectGroup> SubjectGroups { get; init; } = [];

    public string DefaultSubjectGroup { get; init; } = "Diğer";

    /// <summary>
    /// Processing details keyed by <c>"&lt;subject group&gt;:&lt;category code&gt;"</c> or, as a fallback, by
    /// <c>"&lt;category code&gt;"</c> alone.
    /// </summary>
    public IReadOnlyDictionary<string, ProcessingDetails> Processing { get; init; } = new Dictionary<string, ProcessingDetails>();

    public static InventoryPolicy Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<InventoryPolicy>(stream, KvkkJson.Options)
            ?? throw new FormatException($"'{path}' is empty.");
    }
}

public sealed record SubjectGroup(string Name, IReadOnlyList<string> Tables);

/// <summary>The VERBİS fields per data category (Regulation on the Data Controllers' Registry, Art. 9(1)).</summary>
public sealed record ProcessingDetails
{
    /// <summary>Processing purposes ("işleme amaçları").</summary>
    public IReadOnlyList<string> Purposes { get; init; } = [];

    /// <summary>Legal bases under KVKK Art. 5 / Art. 6 (required by the processing inventory, not shown in VERBİS).</summary>
    public IReadOnlyList<string> LegalBases { get; init; } = [];

    /// <summary>Maximum retention period ("azami saklama süresi").</summary>
    public string Retention { get; init; } = string.Empty;

    /// <summary>Recipient groups ("alıcı grupları").</summary>
    public IReadOnlyList<string> Recipients { get; init; } = [];

    /// <summary>Planned transfers abroad ("yabancı ülkelere aktarım").</summary>
    public ForeignTransfer ForeignTransfer { get; init; } = new(false, []);
}

public sealed record ForeignTransfer(bool Planned, IReadOnlyList<string> Countries);

/// <summary>One VERBİS row: a data category processed for a data subject group, with where it was found.</summary>
public sealed record InventoryEntry(
    string SubjectGroup,
    string CategoryCode,
    string Category,
    bool Special,
    IReadOnlyList<string> Purposes,
    IReadOnlyList<string> LegalBases,
    string Retention,
    IReadOnlyList<string> Recipients,
    ForeignTransfer ForeignTransfer,
    IReadOnlyList<string> Sources,
    bool PolicyMissing);

public sealed record VerbisInventory(
    string Controller,
    string Dialect,
    string Schema,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<InventoryEntry> Entries,
    IReadOnlyList<string> Gaps);

public static class KvkkJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Turkish category names stay readable in the JSON instead of becoming \u escapes.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
