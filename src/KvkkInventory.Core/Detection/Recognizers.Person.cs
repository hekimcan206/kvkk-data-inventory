using System.Globalization;
using System.Text.RegularExpressions;
using KvkkInventory.Classification;
using KvkkInventory.Text;

namespace KvkkInventory.Detection;

/// <summary>
/// Person names: 1–4 capitalised words where a word is a known Turkish first name or the last word a known
/// surname. Name rules such as <c>adi</c> are ambiguous (<c>ilac_adi</c> is a drug name), so tokens naming a
/// non-person entity cancel most of the name evidence and value sampling decides.
/// </summary>
public sealed partial class PersonNameRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("ad", 0.8), ("adi", 0.8), ("soyad", 0.9), ("soyadi", 0.9), ("ad soyad", 0.95), ("adsoyad", 0.95),
        ("isim", 0.9), ("ismi", 0.85), ("first name", 0.9), ("last name", 0.9), ("surname", 0.9),
        ("full name", 0.9), ("name", 0.55), ("anne adi", 0.95), ("baba adi", 0.95));

    private static readonly HashSet<string> NonPersonEntities =
    [
        "ilac", "urun", "bolum", "departman", "sirket", "firma", "kurum", "hastane", "sube", "il", "ilce", "sehir",
        "product", "company", "department", "drug", "branch", "city", "file", "dosya", "kategori", "category",
        "kullanici", "user", "host", "server", "tablo", "table", "test", "birim", "unit", "servis", "service",
    ];

    private static readonly HashSet<string> Titles = ["dr", "uzm", "prof", "doc", "op", "hemsire", "bay", "bayan", "sn"];

    public override PiiType Type => PiiType.PersonName;

    public override double ValueStrength => 0.8;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override IReadOnlySet<string> NegativeTokens => NonPersonEntities;

    protected override ValueMatch MatchValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is < 2 or > 60 || !Shape().IsMatch(trimmed))
        {
            return ValueMatch.None;
        }

        var words = trimmed.Split([' ', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(TurkishText.Fold)
            .Where(w => !Titles.Contains(w))
            .ToArray();
        if (words.Length is 0 or > 4)
        {
            return ValueMatch.None;
        }

        return words.Any(Lexicons.FirstNames.Contains) || Lexicons.Surnames.Contains(words[^1])
            ? ValueMatch.Full
            : ValueMatch.None;
    }

    [GeneratedRegex(@"^[\p{L}][\p{L}' .-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}

/// <summary>
/// Date of birth. Any date column would match a "valid date" check, so value evidence requires dates that are
/// plausible birth dates (0–110 years ago) <i>and</i> spread over at least 15 years, which separates birth dates
/// from <c>created_at</c>-style timestamps; it stays below the threshold without a name rule.
/// </summary>
public sealed class DateOfBirthRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("dogum tarihi", 0.95), ("dogum", 0.8), ("dogum yili", 0.85), ("dob", 0.9), ("birth date", 0.95),
        ("date of birth", 0.95), ("birthdate", 0.95), ("birthday", 0.9), ("born", 0.7));

    private static readonly string[] Formats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy", "dd/MM/yyyy"];

    public override PiiType Type => PiiType.DateOfBirth;

    public override double ValueStrength => 0.45;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override (double Score, int Matched) ScoreValues(IReadOnlyList<string> samples)
    {
        var thisYear = DateTime.UtcNow.Year;
        var years = new List<int>(samples.Count);
        foreach (var sample in samples)
        {
            if (DateTime.TryParseExact(sample.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && date.Year >= thisYear - 110 && date.Year <= thisYear)
            {
                years.Add(date.Year);
            }
        }

        if (years.Count == 0 || years.Max() - years.Min() < 15)
        {
            return (0, years.Count);
        }

        var ratio = Math.Min(1, years.Count / (double)samples.Count / 0.8);
        return (ValueStrength * ratio * Math.Min(1, samples.Count / 5.0), years.Count);
    }
}

public sealed class GenderRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(("cinsiyet", 0.95), ("gender", 0.95), ("sex", 0.8));

    public override PiiType Type => PiiType.Gender;

    public override double ValueStrength => 0.55;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;

    protected override ValueMatch MatchValue(string value) =>
        Lexicons.Genders.Contains(TurkishText.Fold(value.Trim())) ? ValueMatch.Full : ValueMatch.None;

    /// <summary>
    /// "E" alone is also <i>evet</i> (yes), "M" a size, "K" a category. A gender column holds both sexes, so value
    /// evidence counts only when values from both sides of one coding scheme appear (E/K, M/F, erkek/kadın …).
    /// </summary>
    protected override (double Score, int Matched) ScoreValues(IReadOnlyList<string> samples)
    {
        var (score, matched) = base.ScoreValues(samples);
        var seen = samples.Select(s => TurkishText.Fold(s.Trim())).ToHashSet(StringComparer.Ordinal);
        var bothSides = (seen.Contains("e") && seen.Contains("k")) || (seen.Contains("m") && seen.Contains("f"))
            || (seen.Contains("erkek") && seen.Contains("kadin")) || (seen.Contains("male") && seen.Contains("female"))
            || (seen.Contains("bay") && seen.Contains("bayan"));
        return bothSides ? (score, matched) : (0, matched);
    }
}

/// <summary>Salary and wage amounts: a number column is not evidence of anything, so names only.</summary>
public sealed class SalaryRecognizer : Recognizer
{
    private static readonly NameRule[] NameRuleSet = Rules(
        ("maas", 0.95), ("net maas", 0.95), ("brut maas", 0.95), ("ucret", 0.75), ("salary", 0.95), ("wage", 0.9),
        ("bordro", 0.6), ("gross pay", 0.9));

    public override PiiType Type => PiiType.Salary;

    protected override IReadOnlyList<NameRule> NameRules => NameRuleSet;
}
