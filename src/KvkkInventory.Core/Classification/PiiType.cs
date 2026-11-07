namespace KvkkInventory.Classification;

/// <summary>
/// What a column contains, as found by a detector. Each type maps to exactly one VERBİS data category
/// (<see cref="KvkkTaxonomy"/>); several types can share a category (e.g. e-mail, phone and address are all
/// "İletişim").
/// </summary>
public enum PiiType
{
    /// <summary>T.C. Kimlik No (11 digits, two check digits).</summary>
    NationalId,

    /// <summary>Vergi Kimlik No (10 digits, one check digit). Personal data when it belongs to a sole proprietor.</summary>
    TaxId,

    PersonName,
    DateOfBirth,
    Gender,
    Email,
    Phone,
    Address,

    /// <summary>IBAN (TR: 26 characters, mod-97).</summary>
    Iban,

    /// <summary>Payment card number (PAN, Luhn), incl. TROY (9792) cards issued in Türkiye.</summary>
    PaymentCard,

    Salary,
    IpAddress,

    /// <summary>Turkish vehicle registration plate, e.g. "34 ABC 123".</summary>
    VehiclePlate,

    // ---- special categories of personal data (KVKK Article 6) ----
    Health,
    Biometric,
    Genetic,
    ReligionBelief,
    PoliticalOpinion,
    EthnicOrigin,
    UnionMembership,
    AssociationMembership,
    CriminalRecord,
}
