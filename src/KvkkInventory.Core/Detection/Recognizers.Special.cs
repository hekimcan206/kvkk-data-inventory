using System.Text.RegularExpressions;
using KvkkInventory.Classification;
using KvkkInventory.Text;

namespace KvkkInventory.Detection;

/// <summary>
/// Health data (KVKK Art. 6): ICD-10 codes, blood groups and clinical vocabulary in values; diagnosis, allergy,
/// prescription, disability … in names. Catalog tables of ICD codes or drugs match too; the classifier drops
/// special-category findings in tables that are not linked to a person (see <see cref="Classifier"/>).
/// </summary>
public sealed partial class HealthRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("tani", 0.9), ("teshis", 0.9), ("diagnosis", 0.9), ("icd", 0.9), ("icd10", 0.95), ("hastalik", 0.9),
        ("kronik", 0.8), ("alerji", 0.9), ("allergy", 0.9), ("kan grubu", 0.95), ("blood type", 0.95),
        ("blood group", 0.95), ("ilac", 0.6), ("recete", 0.8), ("prescription", 0.8), ("tedavi", 0.8),
        ("treatment", 0.7), ("epikriz", 0.95), ("ameliyat", 0.85), ("surgery", 0.8), ("engel", 0.7),
        ("engellilik", 0.9), ("disability", 0.9), ("sikayet", 0.6), ("tahlil", 0.85), ("lab sonuc", 0.85),
        ("medical", 0.8), ("saglik", 0.8), ("health", 0.8), ("gebelik", 0.9), ("hamilelik", 0.9), ("psikiyatri", 0.85));

    public override PiiType Type => PiiType.Health;

    public override double ValueStrength => 0.75;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value)
    {
        var trimmed = value.Trim();
        if (Icd10().IsMatch(trimmed) || BloodGroup().IsMatch(trimmed))
        {
            return ValueMatch.Full;
        }

        foreach (var word in Words().Split(TurkishText.Fold(trimmed)))
        {
            if (Lexicons.HealthTerms.Contains(word))
            {
                return trimmed.Length <= 80 ? ValueMatch.Full : ValueMatch.Embedded;
            }
        }

        return ValueMatch.None;
    }

    [GeneratedRegex(@"^[A-TV-Z]\d{2}(?:\.[0-9A-Z]{1,4})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Icd10();

    [GeneratedRegex(@"^(?:0|A|B|AB)\s*(?:Rh)?\s*[+-]$|^(?:0|A|B|AB)\s+Rh\s*(?:\+|-|pozitif|negatif)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BloodGroup();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();
}

public sealed partial class ReligionBeliefRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("din", 0.9), ("dini", 0.9), ("mezhep", 0.95), ("inanc", 0.9), ("religion", 0.95), ("faith", 0.8),
        ("belief", 0.85), ("felsefi inanc", 0.95));

    public override PiiType Type => PiiType.ReligionBelief;

    public override double ValueStrength => 0.7;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 40 && Words().Split(TurkishText.Fold(trimmed)).Any(Lexicons.Religions.Contains)
            ? ValueMatch.Full
            : ValueMatch.None;
    }

    [GeneratedRegex(@"[^a-z]+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();
}

/// <summary>Biometric templates are opaque binaries; the column name is the only evidence there is.</summary>
public sealed class BiometricRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("parmak izi", 0.95), ("parmakizi", 0.95), ("fingerprint", 0.95), ("biyometrik", 0.95), ("biometric", 0.95),
        ("face template", 0.9), ("yuz sablonu", 0.9), ("yuz tanima", 0.9), ("iris", 0.9), ("retina", 0.9),
        ("avuc ici", 0.9), ("palm vein", 0.9));

    public override PiiType Type => PiiType.Biometric;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}

public sealed class GeneticRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("genetik", 0.95), ("genetic", 0.95), ("dna", 0.95), ("genom", 0.95), ("genome", 0.95), ("gen testi", 0.9));

    public override PiiType Type => PiiType.Genetic;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}

public sealed class PoliticalOpinionRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("siyasi", 0.95), ("siyasi gorus", 0.95), ("parti uyeligi", 0.95), ("parti", 0.45), ("political", 0.95),
        ("party membership", 0.95));

    public override PiiType Type => PiiType.PoliticalOpinion;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}

public sealed class EthnicOriginRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("irk", 0.95), ("etnik", 0.95), ("etnik koken", 0.95), ("ethnicity", 0.95), ("ethnic", 0.95), ("race", 0.85));

    public override PiiType Type => PiiType.EthnicOrigin;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}

public sealed class UnionMembershipRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("sendika", 0.95), ("sendika uyeligi", 0.95), ("trade union", 0.95), ("union member", 0.95), ("union", 0.7));

    public override PiiType Type => PiiType.UnionMembership;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}

public sealed class AssociationMembershipRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("dernek", 0.95), ("dernek uyeligi", 0.95), ("vakif", 0.9), ("vakif uyeligi", 0.95), ("association", 0.85));

    public override PiiType Type => PiiType.AssociationMembership;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}

public sealed class CriminalRecordRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("sabika", 0.95), ("sabika kaydi", 0.95), ("adli sicil", 0.95), ("mahkumiyet", 0.95), ("criminal", 0.95),
        ("conviction", 0.95), ("ceza", 0.5), ("guvenlik tedbiri", 0.9));

    public override PiiType Type => PiiType.CriminalRecord;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}
