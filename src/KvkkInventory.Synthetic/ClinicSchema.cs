using System.Globalization;
using KvkkInventory.Classification;
using KvkkInventory.Schema;
using KvkkInventory.Text;
using static KvkkInventory.Classification.PiiType;

namespace KvkkInventory.Synthetic;

/// <summary>
/// The labelled synthetic "clinic + HR" schema used by the demo, the integration tests and the accuracy benchmark.
/// Column names mix Turkish and English and include the decoys real schemas have: 11-digit numbers that are not
/// TCKNs, <c>*_adi</c> columns that name departments and drugs, business phone numbers, password hashes, catalogs
/// of ICD codes, hire dates that look like birth dates, contextual health data the detectors cannot see.
/// Every column carries the label a human reviewer would give it. All data is synthetic.
/// </summary>
public static class ClinicSchema
{
    private static readonly (string Code, string Text)[] Diagnoses =
    [
        ("I10", "Esansiyel (primer) hipertansiyon"), ("E11.9", "Tip 2 diyabet, komplikasyonsuz"), ("J45.9", "Astım, tanımlanmamış"),
        ("G43.9", "Migren, tanımlanmamış"), ("M54.5", "Bel ağrısı"), ("K29.7", "Gastrit, tanımlanmamış"),
        ("F32.9", "Depresif epizod, tanımlanmamış"), ("J06.9", "Akut üst solunum yolu enfeksiyonu"), ("N39.0", "İdrar yolu enfeksiyonu"),
        ("E78.0", "Saf hiperkolesterolemi"), ("I48", "Atriyal fibrilasyon"), ("H52.1", "Miyopi"), ("L40.0", "Psoriasis vulgaris"),
        ("K35.8", "Akut apandisit"), ("E03.9", "Hipotiroidi, tanımlanmamış"), ("J18.9", "Pnömoni, tanımlanmamış"),
    ];

    private static readonly string[] Drugs =
    [
        "Parol 500 mg", "Arveles 25 mg", "Augmentin 1000 mg", "Coraspin 100 mg", "Glifor 1000 mg", "Ventolin inhaler",
        "Nexium 40 mg", "Lustral 50 mg", "Beloc 50 mg", "Concor 5 mg", "Euthyrox 50 mcg", "Majezik 100 mg",
    ];

    private static readonly string[] Departments =
    [
        "Kardiyoloji", "Dahiliye", "Ortopedi", "Göz Hastalıkları", "Kulak Burun Boğaz", "Nöroloji", "Psikiyatri",
        "Çocuk Sağlığı", "Genel Cerrahi", "Dermatoloji",
    ];

    private static readonly string[] Complaints = ["migren", "hipertansiyon", "bel ağrısı", "astım", "diyabet", "baş dönmesi", "gastrit", "öksürük"];

    private static readonly string[] MailDomains = ["gmail.com", "hotmail.com", "yandex.com", "outlook.com", "icloud.com"];

    private static readonly string[] Unions = ["Sağlık-Sen", "Tüm Bel-Sen", "Dev Sağlık-İş", "Yok", "Yok", "Yok"];

    private static readonly string[] Religions = ["İslam", "İslam", "Hristiyan", "Belirtilmemiş", "Ateist", "Yahudi"];

    private static readonly string[] Associations = ["Türk Kalp Vakfı", "Diyabet Derneği", "Yok", "Yok", "Mezunlar Derneği"];

    private static readonly DateTime Now = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public static IReadOnlyList<SyntheticTable> Tables() =>
    [
        Patients(), Appointments(), DiagnosisRecords(), Prescriptions(), Invoices(), IcdCatalog(), DrugCatalog(), Branches(),
        Employees(), EmployeeLeave(), AuditLog(), Customers(), GeneticTests(), SocialAssessment(),
    ];

    private static SyntheticTable Patients() => new("hastalar",
    [
        Id(),
        Col("tc_kimlik_no", LogicalType.Text, [NationalId], c => SyntheticIdentifiers.Tckn(c.R), length: 11, nullable: false),
        Col("ad", LogicalType.Text, [PersonName], c => NamePool.FirstName(c.F)),
        Col("soyad", LogicalType.Text, [PersonName], c => NamePool.LastName(c.F)),
        Col("dogum_tarihi", LogicalType.Date, [DateOfBirth], c => c.F.Date.Between(new DateTime(1938, 1, 1), new DateTime(2024, 12, 31)).Date),
        Col("cinsiyet", LogicalType.Text, [Gender], c => c.R.Bool() ? "E" : "K", length: 1),
        Col("cep_telefonu", LogicalType.Text, [Phone], c => Sometimes(c, 0.95, () => SyntheticIdentifiers.Phone(c.R, mobileOnly: true)), length: 20),
        Col("eposta", LogicalType.Text, [Email], c => Sometimes(c, 0.8, () => MakeEmail(c))),
        Col("adres", LogicalType.Text, [Address], MakeAddress, length: 250),
        Col("kan_grubu", LogicalType.Text, [Health], c => c.F.PickRandom("A Rh+", "A Rh-", "B Rh+", "0 Rh+", "0 Rh-", "AB Rh+"), length: 8),
        // Decoy: an 11-digit registry number that is not a TCKN (about 1 % pass the checksum by chance).
        Col("kayit_no", LogicalType.Text, [], c => c.R.Number(1, 9).ToString(CultureInfo.InvariantCulture) + c.R.ReplaceNumbers("##########"), length: 11),
        // Decoy: E/H consent flag — "E" is also the Turkish code for male.
        Col("kvkk_onay", LogicalType.Text, [], c => c.R.Bool(0.9f) ? "E" : "H", length: 1),
        Col("created_at", LogicalType.Timestamp, [], c => c.F.Date.Between(Now.AddYears(-3), Now)),
    ], "Hasta kayıtları");

    private static SyntheticTable Appointments() => new("randevular",
    [
        Id(),
        Col("hasta_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 5000)),
        Col("doktor_adi", LogicalType.Text, [PersonName], c => $"Dr. {NamePool.FullName(c.F)}"),
        // Contextual health data: an appointment at psychiatry reveals health; no column-level detector can see it.
        Col("bolum_adi", LogicalType.Text, [Health], c => c.F.PickRandom(Departments)),
        Col("randevu_zamani", LogicalType.Timestamp, [], c => c.F.Date.Between(Now.AddYears(-1), Now.AddMonths(2))),
        // Decoy: "B12" looks like an ICD-10 code.
        Col("oda_no", LogicalType.Text, [], c => $"{c.F.PickRandom("A", "B", "C")}{c.R.Number(1, 40)}", length: 10),
        Col("notlar", LogicalType.LongText, [Phone, Health], MakeNote),
    ]);

    private static SyntheticTable DiagnosisRecords() => new("tanilar",
    [
        Id(),
        Col("hasta_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 5000)),
        Col("icd10_kodu", LogicalType.Text, [Health], c => c.F.PickRandom(Diagnoses).Code, length: 10),
        Col("tani_aciklamasi", LogicalType.Text, [Health], c => c.F.PickRandom(Diagnoses).Text, length: 200),
        Col("kayit_tarihi", LogicalType.Date, [], c => c.F.Date.Between(Now.AddYears(-4), Now).Date),
    ]);

    private static SyntheticTable Prescriptions() => new("receteler",
    [
        Id(),
        Col("hasta_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 5000)),
        // Health data by context (a person's medication); drug names are not in any clinical-term lexicon.
        Col("ilac_adi", LogicalType.Text, [Health], c => c.F.PickRandom(Drugs)),
        Col("doz", LogicalType.Text, [], c => c.F.PickRandom("1x1", "2x1", "3x1", "1x2"), length: 10),
        Col("recete_no", LogicalType.Text, [], c => c.R.AlphaNumeric(7).ToUpperInvariant(), length: 10),
    ]);

    private static SyntheticTable Invoices() => new("faturalar",
    [
        Id(),
        Col("hasta_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 5000)),
        Col("fatura_no", LogicalType.Text, [], c => "GIB2026" + c.R.ReplaceNumbers("#########"), length: 16),
        Col("tutar", LogicalType.Amount, [], c => Math.Round(c.R.Decimal(150, 25000), 2)),
        Col("sigorta_vkn", LogicalType.Text, [TaxId], c => SyntheticIdentifiers.Vkn(c.R), length: 10),
        Col("odeme_karti", LogicalType.Text, [PaymentCard], c => Sometimes(c, 0.6, () => SyntheticIdentifiers.CardNumber(c.R, grouped: true)), length: 19),
    ]);

    // Reference data: no data subject. Health findings here must be suppressed by the linkage rule.
    private static SyntheticTable IcdCatalog() => new("icd_kodlari",
    [
        Col("kod", LogicalType.Text, [], c => Diagnoses[c.Index % Diagnoses.Length].Code, length: 10, nullable: false),
        Col("aciklama", LogicalType.Text, [], c => Diagnoses[c.Index % Diagnoses.Length].Text, length: 200),
    ], "ICD-10 kod listesi (referans)");

    private static SyntheticTable DrugCatalog() => new("ilac_katalogu",
    [
        Col("atc_kodu", LogicalType.Text, [], c => c.F.PickRandom("N02BE01", "M01AE01", "J01CR02", "B01AC06", "A10BA02", "R03AC02"), length: 10),
        Col("ilac_adi", LogicalType.Text, [], c => c.F.PickRandom(Drugs)),
        // Decoy: 13-digit GTIN barcodes (Türkiye prefix 869) — digits, but neither Luhn-checked cards nor TCKNs.
        Col("barkod", LogicalType.Text, [], c => "869" + c.R.ReplaceNumbers("##########"), length: 13),
    ]);

    // Business contact data of the organisation itself: not personal data (expected false positives).
    private static SyntheticTable Branches() => new("subeler",
    [
        Id(),
        Col("sube_adi", LogicalType.Text, [], c => $"{c.F.Address.City()} Şubesi"),
        Col("telefon", LogicalType.Text, [], c => $"0{c.F.PickRandom("212", "216", "312", "232")} {c.R.ReplaceNumbers("### ## ##")}", length: 20),
        Col("adres", LogicalType.Text, [], MakeAddress, length: 250),
    ]);

    private static SyntheticTable Employees() => new("personel",
    [
        Id(),
        Col("tckn", LogicalType.Integer64, [NationalId], c => long.Parse(SyntheticIdentifiers.Tckn(c.R), CultureInfo.InvariantCulture)),
        Col("ad_soyad", LogicalType.Text, [PersonName], c => $"{NamePool.FullName(c.F)}"),
        Col("birth_date", LogicalType.Date, [DateOfBirth], c => c.F.Date.Between(new DateTime(1960, 1, 1), new DateTime(2004, 12, 31)).Date),
        Col("gender", LogicalType.Text, [Gender], c => c.R.Bool() ? "Kadın" : "Erkek", length: 10),
        Col("email", LogicalType.Text, [Email], c => MakeEmail(c)),
        Col("gsm", LogicalType.Text, [Phone], c => SyntheticIdentifiers.Phone(c.R, mobileOnly: true), length: 20),
        Col("ikamet_adresi", LogicalType.Text, [Address], MakeAddress, length: 250),
        Col("iban", LogicalType.Text, [Iban], c => SyntheticIdentifiers.Iban(c.R, grouped: c.R.Bool()), length: 32),
        Col("maas", LogicalType.Amount, [Salary], c => Math.Round(c.R.Decimal(22000, 140000), 2)),
        // Decoy: hire dates span decades and look like birth dates to a value-only detector.
        Col("ise_giris_tarihi", LogicalType.Date, [], c => c.F.Date.Between(new DateTime(1995, 1, 1), Now).Date),
        Col("kan_grubu", LogicalType.Text, [Health], c => c.F.PickRandom("A Rh+", "B Rh+", "0 Rh+", "AB Rh-"), length: 8),
        Col("engel_durumu", LogicalType.Text, [Health], c => c.R.Bool(0.85f) ? "Yok" : $"%{c.R.Number(4, 9)}0 ortopedik engelli"),
        Col("sendika_uyeligi", LogicalType.Text, [UnionMembership], c => c.F.PickRandom(Unions)),
        Col("dernek_uyelikleri", LogicalType.Text, [AssociationMembership], c => c.F.PickRandom(Associations)),
        Col("din", LogicalType.Text, [ReligionBelief], c => c.F.PickRandom(Religions), length: 30),
        Col("sabika_kaydi", LogicalType.Text, [CriminalRecord], c => c.R.Bool(0.95f) ? "Yok" : "Var"),
        Col("parmak_izi_sablonu", LogicalType.Binary, [Biometric], c => c.R.Bytes(64)),
        Col("arac_plakasi", LogicalType.Text, [VehiclePlate], c => Sometimes(c, 0.7, () => SyntheticIdentifiers.Plate(c.R)), length: 12),
        Col("acil_durum_kisi", LogicalType.Text, [PersonName], c => $"{NamePool.FullName(c.F)}"),
        Col("acil_durum_tel", LogicalType.Text, [Phone], c => SyntheticIdentifiers.Phone(c.R), length: 20),
    ], "Personel özlük bilgileri");

    private static SyntheticTable EmployeeLeave() => new("personel_izinleri",
    [
        Id(),
        Col("personel_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 500)),
        Col("izin_turu", LogicalType.Text, [], c => c.F.PickRandom("Yıllık", "Mazeret", "Ücretsiz", "Rapor"), length: 20),
        Col("baslangic_tarihi", LogicalType.Date, [], c => c.F.Date.Between(Now.AddYears(-2), Now).Date),
        Col("bitis_tarihi", LogicalType.Date, [], c => c.F.Date.Between(Now.AddYears(-2), Now).Date),
        Col("rapor_tani_kodu", LogicalType.Text, [Health], c => Sometimes(c, 0.3, () => c.F.PickRandom(Diagnoses).Code), length: 10),
    ]);

    private static SyntheticTable AuditLog() => new("audit_log",
    [
        Id(),
        // An account name is an identifier but not a person's name; the "kullanici" token marks it.
        Col("kullanici_adi", LogicalType.Text, [], c => TurkishText.Fold(NamePool.FirstName(c.F)[..1] + NamePool.LastName(c.F)), length: 40),
        Col("ip_adresi", LogicalType.Text, [IpAddress], c => SyntheticIdentifiers.Ipv4(c.R), length: 45),
        Col("islem", LogicalType.Text, [], c => c.F.PickRandom("LOGIN", "VIEW_PATIENT", "EXPORT", "UPDATE", "LOGOUT"), length: 20),
        Col("zaman", LogicalType.Timestamp, [], c => c.F.Date.Between(Now.AddDays(-90), Now)),
        Col("user_agent", LogicalType.Text, [], c => c.F.Internet.UserAgent(), length: 400),
    ]);

    private static SyntheticTable Customers() => new("customers",
    [
        Id(),
        Col("full_name", LogicalType.Text, [PersonName], c => $"{NamePool.FullName(c.F)}"),
        Col("email_address", LogicalType.Text, [Email], MakeEmail),
        // Decoy: a flag whose name contains "email".
        Col("email_verified", LogicalType.Text, [], c => c.R.Bool() ? "Y" : "N", length: 1),
        Col("phone_number", LogicalType.Text, [Phone], c => SyntheticIdentifiers.Phone(c.R, mobileOnly: true), length: 20),
        Col("card_number", LogicalType.Text, [PaymentCard], c => SyntheticIdentifiers.CardNumber(c.R, grouped: c.R.Bool()), length: 19),
        // Decoys: hashes are derived identifiers, not the value types the detectors look for.
        Col("password_hash", LogicalType.Text, [], c => c.R.Hexadecimal(64, prefix: string.Empty), length: 64),
        Col("tc_hash", LogicalType.Text, [], c => c.R.Hexadecimal(64, prefix: string.Empty), length: 64),
        Col("last_login_ip", LogicalType.Text, [IpAddress], c => SyntheticIdentifiers.Ipv4(c.R), length: 45),
        Col("marketing_consent", LogicalType.Text, [], c => c.R.Bool() ? "Y" : "N", length: 1),
        Col("created_at", LogicalType.Timestamp, [], c => c.F.Date.Between(Now.AddYears(-2), Now)),
    ], "Online portal users");

    private static SyntheticTable GeneticTests() => new("genetik_testler",
    [
        Id(),
        Col("hasta_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 5000)),
        Col("dna_ornek_kodu", LogicalType.Text, [Genetic], c => "DNA-2026-" + c.R.ReplaceNumbers("######"), length: 20),
        // Genetic result under a neutral column name: expected false negative for name-driven detection.
        Col("sonuc", LogicalType.Text, [Genetic], c => c.F.PickRandom("BRCA1 varyantı saptanmadı", "BRCA2 heterozigot varyant", "HFE C282Y homozigot"), length: 100),
    ]);

    private static SyntheticTable SocialAssessment() => new("sosyal_degerlendirme",
    [
        Id(),
        Col("hasta_id", LogicalType.Integer64, [], c => (long)c.R.Number(1, 5000)),
        Col("etnik_koken", LogicalType.Text, [EthnicOrigin], c => c.R.Bool(0.7f) ? "Belirtilmemiş" : $"Kod-0{c.R.Number(1, 6)}", length: 20),
        Col("siyasi_gorus", LogicalType.Text, [PoliticalOpinion], c => c.F.PickRandom("Beyan yok", "Beyan yok", "Beyan edildi"), length: 20),
        Col("mezhep", LogicalType.Text, [ReligionBelief], c => c.F.PickRandom("Belirtilmemiş", "Sünni", "Alevi"), length: 20),
        Col("degerlendirme_tarihi", LogicalType.Date, [], c => c.F.Date.Between(Now.AddYears(-2), Now).Date),
    ], "Sosyal hizmet değerlendirmesi");

    private static SyntheticColumn Id() =>
        new(new ColumnSpec("id", LogicalType.Integer64, Nullable: false, PrimaryKey: true), new HashSet<PiiType>(), c => (long)(c.Index + 1));

    private static SyntheticColumn Col(string name, LogicalType type, PiiType[] labels, Func<RowContext, object?> generate, int length = 100, bool nullable = true) =>
        new(new ColumnSpec(name, type, length, nullable), labels.ToHashSet(), generate);

    private static object? Sometimes(RowContext c, double probability, Func<object?> value) =>
        c.R.Bool((float)probability) ? value() : null;

    private static string MakeEmail(RowContext c)
    {
        var first = TurkishText.Fold(NamePool.FirstName(c.F)).Replace(" ", string.Empty, StringComparison.Ordinal);
        var last = TurkishText.Fold(NamePool.LastName(c.F)).Replace(" ", string.Empty, StringComparison.Ordinal);
        return c.R.Number(0, 2) switch
        {
            0 => $"{first}.{last}@{c.F.PickRandom(MailDomains)}",
            1 => $"{first}{last}{c.R.Number(1, 99)}@{c.F.PickRandom(MailDomains)}",
            _ => $"{first[0]}{last}@{c.F.PickRandom(MailDomains)}",
        };
    }

    private static string MakeAddress(RowContext c)
    {
        var street = c.F.Address.StreetName();
        var city = c.F.Address.City();
        var number = c.R.Number(1, 180);
        return c.R.Number(0, 3) switch
        {
            0 => $"{NamePool.LastName(c.F)} Mah. {street} Sok. No:{number} D:{c.R.Number(1, 20)} {city}",
            1 => $"{street} Cad. No:{number} Kat:{c.R.Number(1, 9)} {city}",
            2 => $"{street} Bulvarı {number}, {city}",
            // Free-form spelling without markers: realistic, and expected to be missed by value detection.
            _ => $"{street} {number} {city}",
        };
    }

    private static string? MakeNote(RowContext c)
    {
        var phone = SyntheticIdentifiers.Phone(c.R, mobileOnly: true);
        var complaint = c.F.PickRandom(Complaints);
        return c.R.Number(0, 4) switch
        {
            0 => $"Hasta {phone} numarasından aranacak, {complaint} şikayeti ile kontrole gelecek.",
            1 => $"Kontrol randevusu. Önceki ziyarette {complaint} tanısı kondu, tedaviye devam.",
            2 => $"Randevu ertelendi, hasta yakını ile {phone} üzerinden görüşüldü.",
            3 => "Sonuçlar hastaya e-posta ile iletilecek, ek tetkik gerekmiyor.",
            _ => null,
        };
    }
}
