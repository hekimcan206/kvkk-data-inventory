using Bogus;
using FsCheck;
using FsCheck.Fluent;
using KvkkInventory.Synthetic;
using KvkkInventory.Validation;

namespace KvkkInventory.UnitTests.Validation;

public sealed class ChecksumTests
{
    [Theory]
    [InlineData("10000000146", true)]   // the widely used test TCKN
    [InlineData("10000000147", false)]  // last check digit wrong
    [InlineData("10000000156", false)]  // first check digit wrong
    [InlineData("01000000146", false)]  // cannot start with 0
    [InlineData("1000000014", false)]   // 10 digits
    [InlineData("1000000014a", false)]
    public void Tckn(string value, bool valid) => Checksums.IsValidTckn(value).Should().Be(valid);

    [Theory]
    [InlineData("3973535717", true)]    // published python-stdnum examples
    [InlineData("2037637860", true)]
    [InlineData("2823097943", true)]
    [InlineData("2012460224", true)]
    [InlineData("3973535711", false)]
    [InlineData("397353571", false)]
    public void Vkn(string value, bool valid) => Checksums.IsValidVkn(value).Should().Be(valid);

    [Theory]
    [InlineData("TR330006100519786457841326", true)]  // ISO 13616 / Wikipedia example
    [InlineData("TR330006100519786457841327", false)]
    [InlineData("TR33000610051978645784132", false)]  // 25 chars: TR IBANs are exactly 26
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("GB82WEST12345698765433", false)]
    public void Iban(string value, bool valid) => Checksums.IsValidIban(value).Should().Be(valid);

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("5555555555554444", true)]
    [InlineData("411111111111", false)]               // 12 digits: too short for a PAN
    public void Luhn(string value, bool valid) => Checksums.IsValidLuhn(value).Should().Be(valid);

    [Fact]
    public void Check_digit_helpers_agree_with_validators()
    {
        Checksums.LuhnCheckDigit("7992739871").Should().Be(3);
        Checksums.VknCheckDigit("397353571").Should().Be(7);
        Checksums.TcknCheckDigits("100000001").Should().Be((4, 6));
        Checksums.IbanCheckDigits("TR", "0006100519786457841326").Should().Be("33");
    }

    [Fact]
    public void Random_eleven_digit_numbers_rarely_pass_the_tckn_check()
    {
        // Why a checksum is strong evidence: measure the false-acceptance rate instead of asserting it from theory.
        var random = new Randomizer(42);
        var passes = Enumerable.Range(0, 100_000)
            .Count(_ => Checksums.IsValidTckn(random.Number(1, 9).ToString(System.Globalization.CultureInfo.InvariantCulture) + random.ReplaceNumbers("##########")));
        (passes / 100_000.0).Should().BeInRange(0.008, 0.012);
    }

    [FsCheck.Xunit.Property(MaxTest = 500)]
    public Property Generated_identifiers_always_validate() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue)), seed =>
        {
            var random = new Randomizer(seed);
            return Checksums.IsValidTckn(SyntheticIdentifiers.Tckn(random))
                && Checksums.IsValidVkn(SyntheticIdentifiers.Vkn(random))
                && Checksums.IsValidIban(SyntheticIdentifiers.Iban(random, grouped: false))
                && Checksums.IsValidLuhn(SyntheticIdentifiers.CardNumber(random, grouped: false));
        });
}
