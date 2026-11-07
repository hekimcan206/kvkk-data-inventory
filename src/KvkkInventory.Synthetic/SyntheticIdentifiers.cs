using System.Globalization;
using System.Text;
using Bogus;
using KvkkInventory.Validation;

namespace KvkkInventory.Synthetic;

/// <summary>
/// Generators for Turkish identifiers that pass their real validation rules (TCKN check digits, VKN check digit,
/// IBAN mod-97, Luhn). They are random numbers with correct checksums, not real people's numbers: a valid
/// checksum says nothing about whether a number has been issued.
/// </summary>
public static class SyntheticIdentifiers
{
    // Public EFT bank codes of large Turkish banks; only the code is real, the accounts are random.
    private static readonly string[] BankCodes = ["00010", "00012", "00015", "00046", "00062", "00064", "00067", "00111"];

    // BIN-like prefixes: Visa, Mastercard and TROY (9792, Türkiye's domestic scheme).
    private static readonly string[] CardPrefixes = ["454360", "540061", "979200", "552879", "415565"];

    private static readonly string[] MobilePrefixes = ["530", "532", "533", "535", "536", "538", "541", "542", "505", "506", "555", "553"];
    private static readonly string[] AreaCodes = ["212", "216", "312", "232", "482", "224", "322", "242"];

    public static string Tckn(Randomizer random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var nine = new StringBuilder(9).Append((char)('1' + random.Number(0, 8)));
        for (var i = 1; i < 9; i++)
        {
            nine.Append((char)('0' + random.Number(0, 9)));
        }

        var (d10, d11) = Checksums.TcknCheckDigits(nine.ToString());
        return string.Create(CultureInfo.InvariantCulture, $"{nine}{d10}{d11}");
    }

    public static string Vkn(Randomizer random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var nine = random.ReplaceNumbers("#########");
        return nine + Checksums.VknCheckDigit(nine).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>TR IBAN, optionally in the printed 4-character grouping.</summary>
    public static string Iban(Randomizer random, bool grouped)
    {
        ArgumentNullException.ThrowIfNull(random);
        var bban = random.ArrayElement(BankCodes) + "0" + random.ReplaceNumbers("################");
        var iban = "TR" + Checksums.IbanCheckDigits("TR", bban) + bban;
        return grouped ? string.Join(' ', iban.Chunk(4).Select(c => new string(c))) : iban;
    }

    public static string CardNumber(Randomizer random, bool grouped)
    {
        ArgumentNullException.ThrowIfNull(random);
        var payload = random.ArrayElement(CardPrefixes) + random.ReplaceNumbers("#########");
        var pan = payload + Checksums.LuhnCheckDigit(payload).ToString(CultureInfo.InvariantCulture);
        return grouped ? string.Join(' ', pan.Chunk(4).Select(c => new string(c))) : pan;
    }

    /// <summary>Turkish phone number in one of the spellings seen in real systems.</summary>
    public static string Phone(Randomizer random, bool mobileOnly = false)
    {
        ArgumentNullException.ThrowIfNull(random);
        var mobile = mobileOnly || random.Bool(0.8f);
        var prefix = mobile ? random.ArrayElement(MobilePrefixes) : random.ArrayElement(AreaCodes);
        var rest = random.ReplaceNumbers("#######");
        return random.Number(0, 3) switch
        {
            0 => $"0{prefix} {rest[..3]} {rest[3..5]} {rest[5..]}",
            1 => $"+90 {prefix} {rest[..3]} {rest[3..5]} {rest[5..]}",
            2 => $"0{prefix}{rest}",
            _ => $"(0{prefix}) {rest[..3]}-{rest[3..]}",
        };
    }

    public static string Plate(Randomizer random)
    {
        ArgumentNullException.ThrowIfNull(random);
        const string letters = "ABCDEFGHJKLMNPRSTUVYZ";
        var province = random.Number(1, 81).ToString("00", CultureInfo.InvariantCulture);
        return random.Number(0, 2) switch
        {
            0 => $"{province} {random.String2(1, letters)} {random.ReplaceNumbers("####")}",
            1 => $"{province} {random.String2(2, letters)} {random.ReplaceNumbers("###")}",
            _ => $"{province} {random.String2(3, letters)} {random.ReplaceNumbers("##")}",
        };
    }

    public static string Ipv4(Randomizer random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return string.Create(CultureInfo.InvariantCulture,
            $"{random.Number(1, 223)}.{random.Number(0, 255)}.{random.Number(0, 255)}.{random.Number(1, 254)}");
    }
}
