using System.Collections.Frozen;
using KvkkInventory.Text;

namespace KvkkInventory.Detection;

/// <summary>
/// Small, hand-curated word lists (folded with <see cref="TurkishText.Fold"/>). They are deliberately independent
/// of the synthetic data generator's name lists, so the measured recall of the name detector is honest: names the
/// generator produces but this list lacks show up as false negatives.
/// </summary>
internal static class Lexicons
{
    public static readonly FrozenSet<string> FirstNames = Set(
        "ahmet", "mehmet", "mustafa", "ali", "huseyin", "hasan", "ibrahim", "ismail", "osman", "yusuf", "murat",
        "omer", "ramazan", "halil", "suleyman", "abdullah", "mahmut", "recep", "salih", "fatih", "kadir", "emre",
        "hakan", "adem", "kemal", "yasar", "bayram", "serkan", "orhan", "burak", "mert", "can", "cem", "kaan",
        "baris", "onur", "volkan", "tolga", "sinan", "erkan", "ugur", "gokhan", "levent", "selim", "taner",
        "tuncay", "enes", "furkan", "emir", "yigit", "arda", "efe", "berk", "deniz", "eren", "umut", "oguz",
        "cengiz", "erdem", "koray", "ozan", "serdar", "sedat", "metin", "nihat", "necati", "ilker", "hamza",
        "hekim", "cihan", "dogan", "tarik", "batuhan", "alper", "anil", "berkay", "caner", "irfan", "kerem",
        "fatma", "ayse", "emine", "hatice", "zeynep", "elif", "meryem", "sultan", "hulya", "ozlem", "esra",
        "merve", "busra", "zehra", "fadime", "havva", "hacer", "sevgi", "leyla", "derya", "ebru", "gul", "gulsen",
        "songul", "aysel", "aynur", "nurten", "yasemin", "seda", "sibel", "tugba", "dilek", "pinar", "burcu",
        "cansu", "ceren", "damla", "ece", "gizem", "irem", "melike", "nazli", "selin", "sinem", "sude", "yagmur",
        "ayten", "bahar", "canan", "duygu", "eda", "elvan", "filiz", "gonul", "hande", "ilknur", "kubra", "lale",
        "melek", "nesrin", "nur", "oya", "rabia", "serap", "sevda", "tulay", "ulku", "yeliz", "zuhal", "asli",
        "beyza", "dilara", "ela", "esma", "hilal", "ipek", "nisa", "ozge", "simge", "tuba", "aleyna", "asya");

    public static readonly FrozenSet<string> Surnames = Set(
        "yilmaz", "kaya", "demir", "sahin", "celik", "yildiz", "yildirim", "ozturk", "aydin", "ozdemir", "arslan",
        "dogan", "kilic", "aslan", "cetin", "kara", "koc", "kurt", "ozkan", "simsek", "polat", "ozcan", "korkmaz",
        "cakir", "erdogan", "yavuz", "can", "acar", "sen", "aktas", "guler", "yalcin", "gunes", "bozkurt", "bulut",
        "keskin", "unal", "turan", "gul", "ozer", "isik", "kaplan", "avci", "sari", "tekin", "tas", "kose",
        "yuksel", "ates", "aksoy", "basaran", "karaca", "toprak", "uysal", "erdem", "guzel", "cinar", "altun",
        "duman", "eren", "akin", "tuncer", "karakaya", "oral", "bayram", "kocak", "coskun", "ekinci", "durmaz",
        "sonmez", "orhan", "tunc", "kahraman", "aksu", "baysal", "gok", "zengin", "akgul", "bilgin", "deniz",
        "aktepe", "karatas", "metin", "ucar", "yazici", "kurtulus", "dinc", "karadag", "oztekin", "ozbek", "ergin");

    /// <summary>The 81 provinces, used by the address detector as a supporting marker.</summary>
    public static readonly FrozenSet<string> Provinces = Set(
        "adana", "adiyaman", "afyonkarahisar", "agri", "amasya", "ankara", "antalya", "artvin", "aydin", "balikesir",
        "bilecik", "bingol", "bitlis", "bolu", "burdur", "bursa", "canakkale", "cankiri", "corum", "denizli",
        "diyarbakir", "edirne", "elazig", "erzincan", "erzurum", "eskisehir", "gaziantep", "giresun", "gumushane",
        "hakkari", "hatay", "isparta", "mersin", "istanbul", "izmir", "kars", "kastamonu", "kayseri", "kirklareli",
        "kirsehir", "kocaeli", "konya", "kutahya", "malatya", "manisa", "kahramanmaras", "mardin", "mugla", "mus",
        "nevsehir", "nigde", "ordu", "rize", "sakarya", "samsun", "siirt", "sinop", "sivas", "tekirdag", "tokat",
        "trabzon", "tunceli", "sanliurfa", "usak", "van", "yozgat", "zonguldak", "aksaray", "bayburt", "karaman",
        "kirikkale", "batman", "sirnak", "bartin", "ardahan", "igdir", "yalova", "karabuk", "kilis", "osmaniye", "duzce");

    public static readonly FrozenSet<string> AddressMarkers = Set(
        "mah", "mahalle", "mahallesi", "sok", "sk", "sokak", "sokagi", "cad", "cd", "cadde", "caddesi", "bulvar",
        "bulvari", "blv", "no", "apt", "apartmani", "daire", "kat", "site", "sitesi", "blok", "kume", "evleri");

    /// <summary>Clinical vocabulary in Turkish and English (diseases, findings, procedures).</summary>
    public static readonly FrozenSet<string> HealthTerms = Set(
        "diyabet", "diabetes", "hipertansiyon", "hypertension", "astim", "asthma", "kanser", "cancer", "tumor",
        "depresyon", "depression", "anksiyete", "anxiety", "migren", "migraine", "grip", "influenza", "pnomoni",
        "pneumonia", "bronsit", "bronchitis", "gastrit", "gastritis", "ulser", "ulcer", "anemi", "anemia",
        "epilepsi", "epilepsy", "alzheimer", "parkinson", "hepatit", "hepatitis", "hiv", "tuberkuloz", "obezite",
        "obesity", "kolesterol", "aritmi", "arrhythmia", "infarktus", "enfarktus", "inme", "stroke", "kirik",
        "fracture", "enfeksiyon", "infection", "sinuzit", "sinusitis", "otit", "tonsillit", "romatoid", "artrit",
        "arthritis", "osteoporoz", "skolyoz", "fitik", "hernia", "bobrek", "yetmezligi", "insufficiency",
        "tiroid", "hipotiroidi", "hipertiroidi", "alerji", "allergy", "egzama", "eczema", "sedef", "psoriasis",
        "gebelik", "pregnancy", "sizofreni", "schizophrenia", "bipolar", "otizm", "autism", "covid", "lenfoma",
        "losemi", "leukemia", "katarakt", "glokom", "glaucoma", "apandisit", "appendicitis", "menisküs",
        "engelli", "ortopedik", "kemoterapi", "chemotherapy", "diyaliz", "dialysis", "ameliyat", "operasyon", "biyopsi", "biopsy");

    public static readonly FrozenSet<string> Religions = Set(
        "islam", "musluman", "hristiyan", "hiristiyan", "christian", "yahudi", "jewish", "alevi", "sunni", "sii",
        "ateist", "atheist", "agnostik", "agnostic", "budist", "buddhist", "deist", "katolik", "catholic",
        "ortodoks", "orthodox", "protestan", "protestant", "ezidi", "suryani");

    public static readonly FrozenSet<string> Genders = Set("e", "k", "m", "f", "erkek", "kadin", "male", "female", "bay", "bayan");

    private static FrozenSet<string> Set(params string[] words) =>
        words.Select(TurkishText.Fold).ToFrozenSet(StringComparer.Ordinal);
}
