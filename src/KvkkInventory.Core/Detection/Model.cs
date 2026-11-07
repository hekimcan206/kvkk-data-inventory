using KvkkInventory.Classification;

namespace KvkkInventory.Detection;

/// <summary>Everything a detector may look at for one column. Samples are text renderings of non-null values.</summary>
/// <param name="Table">Table name as stored in the catalog.</param>
/// <param name="Column">Column name as stored in the catalog.</param>
/// <param name="DataType">Catalog data type (e.g. <c>character varying</c>, <c>VARCHAR2</c>).</param>
/// <param name="Comment">Column comment from the catalog, used as extra context like Presidio's request-level context words.</param>
/// <param name="Samples">Sampled non-null values, rendered as invariant text. Never persisted.</param>
/// <param name="IsBinary">True for BLOB/bytea columns: no value sampling, name evidence only.</param>
public sealed record ColumnContext(
    string Table,
    string Column,
    string? DataType,
    string? Comment,
    IReadOnlyList<string> Samples,
    bool IsBinary = false);

/// <summary>
/// One detector's verdict on one column. Carries counts and scores, never the sampled values, so scan reports
/// can be stored and shared without becoming a copy of the personal data they describe.
/// </summary>
public sealed record Evidence(
    PiiType Type,
    double Confidence,
    double NameScore,
    double ValueScore,
    int Matched,
    int Sampled,
    string Reason);

/// <summary>Which evidence sources the engine uses; switched off one at a time for the ablation table.</summary>
public sealed record DetectionOptions
{
    public static readonly DetectionOptions Default = new();

    public bool UseNames { get; init; } = true;

    public bool UseValues { get; init; } = true;

    /// <summary>Evidence below this confidence is dropped from scan output entirely (not just unclassified).</summary>
    public double ReportFloor { get; init; } = 0.2;
}

/// <summary>Result of matching a single value against a recognizer's pattern and validator.</summary>
public enum ValueMatch
{
    None,

    /// <summary>The whole value is an instance (e.g. the value is a valid TCKN).</summary>
    Full,

    /// <summary>The value contains an instance inside free text (e.g. a phone number inside a note).</summary>
    Embedded,
}
