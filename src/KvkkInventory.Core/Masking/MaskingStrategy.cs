using KvkkInventory.Classification;

namespace KvkkInventory.Masking;

public enum MaskingStrategy
{
    /// <summary>Leave the value as it is (low-risk attributes such as gender in a test copy).</summary>
    Keep,

    /// <summary>Remove the value. The default for special categories: test environments rarely need them.</summary>
    Null,

    /// <summary>Replace with an opaque keyed token (HMAC-SHA256), e.g. <c>tok_4KQ2…</c>; joins still work.</summary>
    HmacToken,

    /// <summary>
    /// Replace with a keyed pseudonym of the same shape that still passes validation: a TCKN with correct check
    /// digits, an IBAN with correct mod-97, a Luhn-valid card number, a phone number with the same prefix.
    /// </summary>
    FormatPreserving,

    /// <summary>Keep only the last characters, e.g. <c>************4821</c> (PCI DSS style truncation).</summary>
    Partial,

    /// <summary>Reduce precision: birth date → year, IPv4 → /24, address → province, salary → 5 000 band.</summary>
    Generalize,

    /// <summary>Replace with a realistic keyed fake (names, addresses) that keeps the data readable for testers.</summary>
    Synthetic,
}

/// <summary>Default strategy per type, chosen for a test/QA copy of production (see ADR 0003).</summary>
public static class MaskingDefaults
{
    public static MaskingStrategy For(PiiType type) => type switch
    {
        PiiType.NationalId or PiiType.TaxId or PiiType.Iban or PiiType.Phone or PiiType.Email or PiiType.VehiclePlate
            => MaskingStrategy.FormatPreserving,
        PiiType.PersonName or PiiType.Address => MaskingStrategy.Synthetic,
        PiiType.DateOfBirth or PiiType.Salary or PiiType.IpAddress => MaskingStrategy.Generalize,
        PiiType.PaymentCard => MaskingStrategy.Partial,
        PiiType.Gender => MaskingStrategy.Keep,
        _ when KvkkTaxonomy.IsSpecial(type) => MaskingStrategy.Null,
        _ => MaskingStrategy.HmacToken,
    };
}
