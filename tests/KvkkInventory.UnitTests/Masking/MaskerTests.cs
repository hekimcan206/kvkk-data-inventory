using System.Globalization;
using Bogus;
using FsCheck;
using FsCheck.Fluent;
using KvkkInventory.Classification;
using KvkkInventory.Masking;
using KvkkInventory.Synthetic;
using KvkkInventory.Validation;

namespace KvkkInventory.UnitTests.Masking;

public sealed class MaskerTests
{
    private static readonly byte[] KeyA = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];
    private static readonly byte[] KeyB = [.. Enumerable.Range(0, 32).Select(i => (byte)(255 - i))];
    private static readonly Masker A = new(KeyA);
    private static readonly Masker B = new(KeyB);

    private static Gen<string> Tckns => Gen.Choose(0, int.MaxValue).Select(seed => SyntheticIdentifiers.Tckn(new Randomizer(seed)));

    [Fact]
    public void Keys_shorter_than_256_bits_are_rejected()
    {
        var act = () => new Masker(new byte[16]);
        act.Should().Throw<ArgumentException>();
    }

    [FsCheck.Xunit.Property(MaxTest = 1000)]
    public Property Format_preserving_tckn_still_passes_the_checksum() =>
        Prop.ForAll(Arb.From(Tckns), tckn =>
        {
            var masked = A.MaskText(tckn, PiiType.NationalId, MaskingStrategy.FormatPreserving);
            return Checksums.IsValidTckn(masked) && masked != tckn;
        });

    [FsCheck.Xunit.Property(MaxTest = 500)]
    public Property Format_preserving_iban_card_and_vkn_stay_valid() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue)), seed =>
        {
            var random = new Randomizer(seed);
            var iban = SyntheticIdentifiers.Iban(random, grouped: true);
            var card = SyntheticIdentifiers.CardNumber(random, grouped: false);
            var maskedIban = A.MaskText(iban, PiiType.Iban, MaskingStrategy.FormatPreserving);
            var maskedCard = A.MaskText(card, PiiType.PaymentCard, MaskingStrategy.FormatPreserving);
            var maskedVkn = A.MaskText(SyntheticIdentifiers.Vkn(random), PiiType.TaxId, MaskingStrategy.FormatPreserving);
            return Checksums.IsValidIban(maskedIban.Replace(" ", string.Empty, StringComparison.Ordinal))
                && maskedIban.Length == iban.Length
                && maskedIban[..9] == iban[..2] + maskedIban[2..4] + iban[4..9] // country + bank code kept
                && Checksums.IsValidLuhn(maskedCard) && maskedCard[..6] == card[..6]
                && Checksums.IsValidVkn(maskedVkn);
        });

    [FsCheck.Xunit.Property(MaxTest = 500)]
    public Property Masking_is_deterministic_and_key_dependent() =>
        Prop.ForAll(Arb.From(Tckns), tckn =>
            A.MaskText(tckn, PiiType.NationalId, MaskingStrategy.FormatPreserving) == A.MaskText(tckn, PiiType.NationalId, MaskingStrategy.FormatPreserving)
            && A.Token(tckn, PiiType.NationalId) != B.Token(tckn, PiiType.NationalId));

    [Fact]
    public void Equivalent_spellings_mask_to_the_same_pseudonym()
    {
        var a = A.MaskText("0532 111 22 33", PiiType.Phone, MaskingStrategy.HmacToken);
        var b = A.MaskText("+90 (532) 111-22-33", PiiType.Phone, MaskingStrategy.HmacToken);
        a.Should().Be(b);
        A.MaskText("Ayşe Yılmaz", PiiType.PersonName, MaskingStrategy.Synthetic)
            .Should().BeEquivalentTo(A.MaskText("AYŞE  YILMAZ", PiiType.PersonName, MaskingStrategy.Synthetic), "case and spacing are normalised before keying");
    }

    [Fact]
    public void Same_value_masks_differently_per_type()
    {
        // Domain separation: a number that is a TCKN in one table and a registry number in another must not link.
        A.Token("10000000146", PiiType.NationalId).Should().NotBe(A.Token("10000000146", PiiType.Phone));
    }

    [Fact]
    public void Without_the_key_a_dictionary_attack_recovers_nothing()
    {
        // The attacker knows the algorithm and the candidate population (here: 20 000 valid TCKNs), but not the key.
        var random = new Randomizer(7);
        var population = Enumerable.Range(0, 20_000).Select(_ => SyntheticIdentifiers.Tckn(random)).Distinct().ToArray();
        var published = population.Take(2_000).Select(t => A.MaskText(t, PiiType.NationalId, MaskingStrategy.FormatPreserving)).ToHashSet();

        var guessedKey = new Masker(KeyB);
        var recovered = population.Count(t => published.Contains(guessedKey.MaskText(t, PiiType.NationalId, MaskingStrategy.FormatPreserving)));

        recovered.Should().BeLessThanOrEqualTo(1, "a wrong key gives unrelated pseudonyms; at most a chance collision");
    }

    [Fact]
    public void Collision_rate_of_format_preserving_tckn_is_measured()
    {
        // 9 free digits (first non-zero) → 9×10^8 pseudonyms: collisions follow the birthday bound n²/2N.
        var random = new Randomizer(11);
        var inputs = Enumerable.Range(0, 100_000).Select(_ => SyntheticIdentifiers.Tckn(random)).Distinct().ToArray();
        var outputs = inputs.Select(t => A.MaskText(t, PiiType.NationalId, MaskingStrategy.FormatPreserving)).ToArray();
        var collisions = outputs.Length - outputs.Distinct().Count();

        var expected = inputs.Length * (double)inputs.Length / (2 * 9e8);
        collisions.Should().BeLessThan((int)(expected * 4) + 5, $"expected ≈ {expected:F1} collisions for {inputs.Length} values");
    }

    [Theory]
    [InlineData(PiiType.PaymentCard, "4543 6012 3456 7890", "**** **** **** 7890")]
    [InlineData(PiiType.Email, "ayse.kaya@gmail.com", "a***@gmail.com")]
    [InlineData(PiiType.Iban, "TR330006100519786457841326", "**********************1326")]
    public void Partial_masking_keeps_only_the_tail(PiiType type, string input, string expected) =>
        A.MaskText(input, type, MaskingStrategy.Partial).Should().Be(expected);

    [Theory]
    [InlineData(PiiType.DateOfBirth, "1984-06-17", "1984-01-01")]
    [InlineData(PiiType.IpAddress, "88.241.17.203", "88.241.17.0")]
    [InlineData(PiiType.Salary, "48250.75", "45000")]
    [InlineData(PiiType.Address, "Lale Mah. Gül Sok. No:5 D:3 Kadıköy/İstanbul", "Istanbul")]
    public void Generalisation_reduces_precision(PiiType type, string input, string expected) =>
        A.MaskText(input, type, MaskingStrategy.Generalize).Should().Be(expected);

    [Fact]
    public void Database_values_keep_their_clr_type()
    {
        A.Mask(10000000146L, PiiType.NationalId, MaskingStrategy.FormatPreserving).Should().BeOfType<long>();
        A.Mask(10000000146m, PiiType.NationalId, MaskingStrategy.FormatPreserving).Should().BeOfType<decimal>();
        A.Mask(new DateTime(1984, 6, 17), PiiType.DateOfBirth, MaskingStrategy.Generalize).Should().Be(new DateTime(1984, 1, 1));
        A.Mask(new DateOnly(1984, 6, 17), PiiType.DateOfBirth, MaskingStrategy.Generalize).Should().Be(new DateOnly(1984, 1, 1));
        A.Mask("x", PiiType.Health, MaskingStrategy.Null).Should().BeNull();
        A.Mask(null, PiiType.Email, MaskingStrategy.FormatPreserving).Should().BeNull();
        A.Mask("tok", PiiType.Email, MaskingStrategy.HmacToken, maxLength: 8).Should().BeOfType<string>().Which.Should().HaveLength(8);
    }

    [Fact]
    public void Masked_values_never_contain_the_original()
    {
        var random = new Randomizer(3);
        for (var i = 0; i < 1000; i++)
        {
            var email = $"user{random.Number(1000, 9999).ToString(CultureInfo.InvariantCulture)}@gmail.com";
            var masked = A.MaskText(email, PiiType.Email, MaskingStrategy.FormatPreserving);
            masked.Should().EndWith("@example.invalid").And.NotContain(email[..8]);
        }
    }
}
