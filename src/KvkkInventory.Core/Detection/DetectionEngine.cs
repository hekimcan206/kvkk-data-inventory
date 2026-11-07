namespace KvkkInventory.Detection;

/// <summary>Runs every recognizer over a column and returns the evidence above the report floor, strongest first.</summary>
public sealed class DetectionEngine
{
    public DetectionEngine(DetectionOptions? options = null, IEnumerable<Recognizer>? recognizers = null)
    {
        Options = options ?? DetectionOptions.Default;
        Recognizers = recognizers?.ToArray() ?? DefaultRecognizers();
    }

    public DetectionOptions Options { get; }

    public IReadOnlyList<Recognizer> Recognizers { get; }

    public static Recognizer[] DefaultRecognizers() =>
    [
        new NationalIdRecognizer(), new TaxIdRecognizer(), new PersonNameRecognizer(), new DateOfBirthRecognizer(),
        new GenderRecognizer(), new EmailRecognizer(), new PhoneRecognizer(), new AddressRecognizer(),
        new IbanRecognizer(), new PaymentCardRecognizer(), new SalaryRecognizer(), new IpAddressRecognizer(),
        new VehiclePlateRecognizer(), new HealthRecognizer(), new BiometricRecognizer(), new GeneticRecognizer(),
        new ReligionBeliefRecognizer(), new PoliticalOpinionRecognizer(), new EthnicOriginRecognizer(),
        new UnionMembershipRecognizer(), new AssociationMembershipRecognizer(), new CriminalRecordRecognizer(),
    ];

    public IReadOnlyList<Evidence> Detect(ColumnContext column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var results = new List<Evidence>();
        foreach (var recognizer in Recognizers)
        {
            if (recognizer.Evaluate(column, Options) is { } evidence && evidence.Confidence >= Options.ReportFloor)
            {
                results.Add(evidence);
            }
        }

        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results;
    }
}
