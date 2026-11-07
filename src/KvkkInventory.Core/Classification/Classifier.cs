using KvkkInventory.Masking;
using KvkkInventory.Scanning;
using KvkkInventory.Text;

namespace KvkkInventory.Classification;

/// <summary>Whether a table's rows are about identifiable people.</summary>
public enum SubjectLinkage
{
    /// <summary>The table itself holds a direct identifier (TCKN, name, e-mail, phone, IBAN, card).</summary>
    Direct,

    /// <summary>The table references a person table (<c>hasta_id</c>, <c>employee_id</c> …).</summary>
    Indirect,

    /// <summary>No link to a person: a catalog / reference table (ICD codes, drug list).</summary>
    None,
}

public sealed record ClassifiedColumn(
    string Table,
    string Column,
    PiiType Type,
    string Category,
    string CategoryName,
    bool Special,
    double Confidence,
    SubjectLinkage Linkage,
    MaskingStrategy Masking,
    string Reason);

public sealed record SuppressedFinding(string Table, string Column, PiiType Type, double Confidence, string Reason);

/// <summary>Output of <c>kvkk classify</c>: personal-data columns with KVKK category and default masking.</summary>
public sealed record ClassificationReport(
    string Dialect,
    string Schema,
    DateTimeOffset ClassifiedAt,
    double Threshold,
    int TablesScanned,
    int ColumnsScanned,
    IReadOnlyList<ClassifiedColumn> Columns,
    IReadOnlyList<SuppressedFinding> Suppressed)
{
    /// <summary>Predicted label set per column, the unit the evaluation compares against ground truth.</summary>
    public ILookup<(string Table, string Column), PiiType> Labels() =>
        Columns.ToLookup(c => (c.Table.ToUpperInvariant(), c.Column.ToUpperInvariant()), c => c.Type);
}

public sealed record ClassifierOptions
{
    public double Threshold { get; init; } = 0.5;

    /// <summary>Drop special-category findings in tables that are not about people (ablation switch).</summary>
    public bool RequireSubjectLink { get; init; } = true;
}

/// <summary>
/// Turns scan evidence into KVKK classifications. Two decisions happen here rather than in the detectors:
/// the confidence threshold, and <b>data-subject linkage</b>. "Diabetes" in a patient's diagnosis column is
/// health data; "Diabetes" in an ICD-10 code list is reference data. A special-category finding only counts when
/// its table is about people — directly (it holds an identifier) or through a person foreign key.
/// </summary>
public static class Classifier
{
    private static readonly HashSet<string> PersonEntities =
    [
        "hasta", "patient", "personel", "employee", "calisan", "musteri", "customer", "kullanici", "user", "uye",
        "member", "kisi", "person", "ogrenci", "student", "doktor", "doctor", "hekim", "sahibi", "owner", "sofor",
        "driver", "aday", "candidate", "veli", "guardian", "basvuru", "applicant",
    ];

    public static ClassificationReport Classify(ScanReport scan, ClassifierOptions? options = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(scan);
        options ??= new ClassifierOptions();
        var columns = new List<ClassifiedColumn>();
        var suppressed = new List<SuppressedFinding>();

        foreach (var table in scan.Tables)
        {
            var linkage = LinkageOf(table, options.Threshold);
            foreach (var column in table.Columns)
            {
                foreach (var evidence in column.Evidence.Where(e => e.Confidence >= options.Threshold))
                {
                    if (options.RequireSubjectLink && linkage == SubjectLinkage.None && KvkkTaxonomy.IsSpecial(evidence.Type))
                    {
                        suppressed.Add(new SuppressedFinding(table.Name, column.Name, evidence.Type, evidence.Confidence,
                            "special category in a table with no data-subject link (reference data?)"));
                        continue;
                    }

                    var category = KvkkTaxonomy.CategoryOf(evidence.Type);
                    columns.Add(new ClassifiedColumn(table.Name, column.Name, evidence.Type, category.Code, category.Name,
                        category.Special, evidence.Confidence, linkage, MaskingDefaults.For(evidence.Type), evidence.Reason));
                }
            }
        }

        return new ClassificationReport(scan.Dialect, scan.Schema, (time ?? TimeProvider.System).GetUtcNow(), options.Threshold,
            scan.Tables.Count, scan.ColumnCount, columns, suppressed);
    }

    internal static SubjectLinkage LinkageOf(TableScan table, double threshold)
    {
        if (table.Columns.Any(c => c.Evidence.Any(e => e.Confidence >= threshold && KvkkTaxonomy.IsDirectIdentifier(e.Type))))
        {
            return SubjectLinkage.Direct;
        }

        foreach (var column in table.Columns)
        {
            var tokens = TurkishText.Tokenize(column.Name);
            if (tokens.Count >= 2 && tokens[^1] is "id" or "no" or "kodu" or "ref" && tokens.Take(tokens.Count - 1).Any(PersonEntities.Contains))
            {
                return SubjectLinkage.Indirect;
            }
        }

        return SubjectLinkage.None;
    }
}
