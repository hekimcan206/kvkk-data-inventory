using Bogus;

namespace KvkkInventory.Synthetic;

/// <summary>
/// Person-name source for the synthetic data. Bogus' Turkish locale draws from a long list of rare, historical
/// names ("Aşıkbulmuş", "Bongulboğa"); real registers are dominated by common modern names. The pool mixes
/// common names (75 %, this list) with Bogus names (25 %), so the name detector is measured on a realistic but
/// not lexicon-friendly mix: this list was written separately from the detector's lexicon and they only partly overlap.
/// </summary>
public static class NamePool
{
    private static readonly string[] CommonFirstNames =
    [
        "Mehmet", "Mustafa", "Ahmet", "Ali", "Hüseyin", "Hasan", "İbrahim", "Murat", "Ömer", "Yusuf", "Emre", "Burak",
        "Eymen", "Aras", "Miraç", "Çınar", "Kuzey", "Poyraz", "Alparslan", "Yağız", "Göktuğ", "Doruk", "Kerem", "Barış",
        "Fatma", "Ayşe", "Emine", "Hatice", "Zeynep", "Elif", "Meryem", "Şerife", "Zehra", "Esra", "Merve", "Büşra",
        "Defne", "Asel", "Azra", "Eylül", "Ecrin", "Nehir", "Masal", "Öykü", "Duru", "Gökçe", "Nilay", "Şule",
        "Tuğba", "Özlem", "Hülya", "Derya", "Sibel", "Gamze", "Pınar", "Cansu", "Irmak", "Melis", "Begüm", "Sena",
    ];

    private static readonly string[] CommonSurnames =
    [
        "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Yıldırım", "Öztürk", "Aydın", "Özdemir", "Arslan",
        "Doğan", "Kılıç", "Aslan", "Çetin", "Kara", "Koç", "Kurt", "Özkan", "Şimşek", "Polat", "Korkmaz", "Erdem",
        "Aktaş", "Taşdemir", "Karakuş", "Bozdağ", "Altıntaş", "Çakmak", "Ekinci", "Gündoğdu", "Tatlı", "Uçar", "Soylu",
    ];

    public static string FirstName(Faker faker)
    {
        ArgumentNullException.ThrowIfNull(faker);
        return faker.Random.Bool(0.75f) ? faker.PickRandom(CommonFirstNames) : faker.Name.FirstName();
    }

    public static string LastName(Faker faker)
    {
        ArgumentNullException.ThrowIfNull(faker);
        return faker.Random.Bool(0.75f) ? faker.PickRandom(CommonSurnames) : faker.Name.LastName().Trim();
    }

    public static string FullName(Faker faker) => FirstName(faker) + " " + LastName(faker);
}
