using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Text;

namespace KvkkInventory.UnitTests.Detection;

public sealed class RecognizerTests
{
    private static readonly DetectionEngine Engine = new();

    private static IReadOnlyList<PiiType> Detect(string column, IEnumerable<string> samples, string? comment = null, bool binary = false) =>
        [.. Engine.Detect(new ColumnContext("t", column, "varchar", comment, [.. samples], binary)).Where(e => e.Confidence >= 0.5).Select(e => e.Type)];

    private static IEnumerable<string> Repeat(params string[] values) => Enumerable.Range(0, 40).Select(i => values[i % values.Length]);

    [Theory]
    [InlineData("DOGUM_TARIHI", new[] { "dogum", "tarihi" })]
    [InlineData("hastaTcKimlikNo", new[] { "hasta", "tc", "kimlik", "no" })]
    [InlineData("İkamet Adresi", new[] { "ikamet", "adresi" })]
    [InlineData("address2", new[] { "address", "2" })]
    public void Identifiers_tokenize_and_fold(string identifier, string[] expected) =>
        TurkishText.Tokenize(identifier).Should().Equal(expected);

    [Fact]
    public void Valid_tckn_values_are_found_under_any_column_name()
    {
        Detect("ref_col", Repeat("10000000146", "12345678950", "98765432150")).Should().Contain(PiiType.NationalId);
    }

    [Fact]
    public void Eleven_digit_numbers_without_checksum_are_not_tckn_even_when_named_so()
    {
        // Name says TCKN, values contradict it: 40 samples, none valid.
        Detect("tc_kimlik_no", Repeat("12345678901", "23456789012", "34567890123")).Should().NotContain(PiiType.NationalId);
    }

    [Fact]
    public void An_empty_column_named_tckn_is_still_flagged_from_the_name()
    {
        Detect("TCKN", []).Should().Contain(PiiType.NationalId, "a strong name with nothing to contradict it is enough to flag for review");
    }

    [Fact]
    public void Email_flags_are_not_email()
    {
        Detect("email_verified", Repeat("Y", "N")).Should().BeEmpty();
        Detect("email_address", Repeat("a.kaya@gmail.com", "b.demir@hotmail.com")).Should().Contain(PiiType.Email);
    }

    [Fact]
    public void Drug_and_department_names_are_not_person_names()
    {
        Detect("ilac_adi", Repeat("Parol 500 mg", "Arveles 25 mg")).Should().NotContain(PiiType.PersonName);
        Detect("bolum_adi", Repeat("Kardiyoloji", "Dahiliye")).Should().NotContain(PiiType.PersonName);
        Detect("hasta_adi", Repeat("Ayşe", "Mehmet", "Zeynep")).Should().Contain(PiiType.PersonName);
    }

    [Fact]
    public void Single_sided_codes_are_not_gender()
    {
        Detect("onay", Repeat("E", "E", "H")).Should().NotContain(PiiType.Gender);
        Detect("cinsiyet", Repeat("E", "K")).Should().Contain(PiiType.Gender);
    }

    [Fact]
    public void Free_text_counts_embedded_phone_numbers()
    {
        var notes = Repeat("Hasta 0532 111 22 33 numarasından aranacak, kontrol randevusu verildi.", "Sonuçlar normal, ek tetkik gerekmiyor bugün.");
        Detect("notlar", notes).Should().Contain(PiiType.Phone);
    }

    [Fact]
    public void Column_comments_act_as_context()
    {
        // A third of the values look like ICD-10 codes: too weak alone, enough with a comment saying so.
        var codes = Repeat("I10", "AB", "CD");
        Detect("kod", codes).Should().NotContain(PiiType.Health);
        Detect("kod", codes, comment: "Hastanın tanı kodu (ICD-10)").Should().Contain(PiiType.Health);
    }

    [Fact]
    public void Hire_dates_are_not_birth_dates_but_named_birth_dates_are()
    {
        var dates = Repeat("1996-03-01", "2004-09-15", "2018-01-10", "1999-07-21", "2023-05-02");
        Detect("ise_giris_tarihi", dates).Should().NotContain(PiiType.DateOfBirth);
        Detect("dogum_tarihi", dates).Should().Contain(PiiType.DateOfBirth);
        Detect("created_at", Repeat("2026-01-03", "2025-11-30")).Should().BeEmpty();
    }

    [Fact]
    public void Binary_biometric_columns_are_found_by_name()
    {
        Detect("PARMAK_IZI_SABLONU", [], binary: true).Should().Equal(PiiType.Biometric);
    }

    [Theory]
    [InlineData("34 ABC 123", true)]
    [InlineData("06 A 1234", true)]
    [InlineData("82 AB 123", false)]   // no province 82
    [InlineData("34 QWX 12", false)]   // Q, W, X are not used
    public void Vehicle_plates(string plate, bool expected) =>
        Detect("arac", Repeat(plate)).Contains(PiiType.VehiclePlate).Should().Be(expected);

    [Theory]
    [InlineData("0532 111 22 33")]
    [InlineData("+90 532 111 22 33")]
    [InlineData("(0212) 345-6789")]
    [InlineData("5321112233")]
    public void Turkish_phone_spellings(string phone) => PhoneRecognizer.IsPhone(phone).Should().BeTrue();

    [Theory]
    [InlineData("3973535717")]  // a VKN: bare 10 digits starting with 3 is not a phone number
    [InlineData("12345")]
    [InlineData("0090 1 23")]
    public void Not_phone_numbers(string value) => PhoneRecognizer.IsPhone(value).Should().BeFalse();

    [Fact]
    public void Evidence_explains_itself_without_values()
    {
        var evidence = Engine.Detect(new ColumnContext("t", "tckn", "varchar", null, [.. Repeat("10000000146")]))[0];
        evidence.Reason.Should().Contain("'tckn'").And.Contain("40/40").And.NotContain("10000000146");
    }
}
