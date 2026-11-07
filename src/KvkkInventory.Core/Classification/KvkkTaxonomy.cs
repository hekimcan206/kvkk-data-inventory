namespace KvkkInventory.Classification;

/// <summary>A VERBİS personal-data category ("kişisel veri kategorisi") with its Turkish label as shown in VERBİS.</summary>
/// <param name="Code">Stable English identifier used in JSON and code.</param>
/// <param name="Name">Turkish label used in the VERBİS registry.</param>
/// <param name="Special">True for special categories of personal data (KVKK Article 6).</param>
public sealed record DataCategory(string Code, string Name, bool Special);

/// <summary>
/// The mapping from detected <see cref="PiiType"/> to KVKK / VERBİS categories.
/// KVKK (Law No. 6698) Article 6(1) lists special categories exhaustively: race, ethnic origin, political opinion,
/// philosophical belief, religion, sect or other beliefs, appearance and dress, membership of associations,
/// foundations or trade unions, health, sexual life, criminal conviction and security measures, biometric and
/// genetic data. Processing them is prohibited unless an Article 6 condition applies, so they are flagged
/// separately and default to the strictest masking.
/// </summary>
public static class KvkkTaxonomy
{
    public static readonly DataCategory Identity = new("identity", "Kimlik", false);
    public static readonly DataCategory Contact = new("contact", "İletişim", false);
    public static readonly DataCategory Location = new("location", "Lokasyon", false);
    public static readonly DataCategory Finance = new("finance", "Finans", false);
    public static readonly DataCategory Personnel = new("personnel", "Özlük", false);
    public static readonly DataCategory TransactionSecurity = new("transaction-security", "İşlem Güvenliği", false);
    public static readonly DataCategory Health = new("health", "Sağlık Bilgileri", true);
    public static readonly DataCategory Biometric = new("biometric", "Biyometrik Veri", true);
    public static readonly DataCategory Genetic = new("genetic", "Genetik Veri", true);
    public static readonly DataCategory Belief = new("belief", "Felsefi İnanç, Din, Mezhep ve Diğer İnançlar", true);
    public static readonly DataCategory Political = new("political", "Siyasi Düşünce", true);
    public static readonly DataCategory Ethnicity = new("ethnicity", "Irk ve Etnik Köken", true);
    public static readonly DataCategory Union = new("union", "Sendika Üyeliği", true);
    public static readonly DataCategory Association = new("association", "Dernek Üyeliği", true);
    public static readonly DataCategory Criminal = new("criminal", "Ceza Mahkûmiyeti ve Güvenlik Tedbirleri", true);

    public static DataCategory CategoryOf(PiiType type) => type switch
    {
        PiiType.NationalId or PiiType.TaxId or PiiType.PersonName or PiiType.DateOfBirth or PiiType.Gender => Identity,
        PiiType.Email or PiiType.Phone or PiiType.Address => Contact,
        PiiType.Iban or PiiType.PaymentCard => Finance,
        PiiType.Salary => Personnel,
        PiiType.IpAddress => TransactionSecurity,
        PiiType.VehiclePlate => Location,
        PiiType.Health => Health,
        PiiType.Biometric => Biometric,
        PiiType.Genetic => Genetic,
        PiiType.ReligionBelief => Belief,
        PiiType.PoliticalOpinion => Political,
        PiiType.EthnicOrigin => Ethnicity,
        PiiType.UnionMembership => Union,
        PiiType.AssociationMembership => Association,
        PiiType.CriminalRecord => Criminal,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped PII type."),
    };

    public static bool IsSpecial(PiiType type) => CategoryOf(type).Special;

    /// <summary>
    /// Direct identifiers single out a person on their own; a table that contains one is "about people", which is
    /// what makes a health code in the same table health data rather than reference data.
    /// </summary>
    public static bool IsDirectIdentifier(PiiType type) =>
        type is PiiType.NationalId or PiiType.PersonName or PiiType.Email or PiiType.Phone or PiiType.Iban or PiiType.PaymentCard;
}
