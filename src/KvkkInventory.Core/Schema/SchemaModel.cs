namespace KvkkInventory.Schema;

/// <summary>Coarse storage class of a column, decided by the dialect from the catalog data type.</summary>
public enum ColumnKind
{
    Text,
    Number,
    Temporal,

    /// <summary>bytea / BLOB / RAW: never sampled, name evidence only.</summary>
    Binary,

    /// <summary>CLOB-like large text: not sampled (cost), name evidence only.</summary>
    LargeText,
    Other,
}

/// <summary>A column as read from the database catalog (ALL_TAB_COLUMNS / information_schema.columns).</summary>
public sealed record ColumnInfo(string Name, string DataType, ColumnKind Kind, int? MaxLength, bool Nullable, int Ordinal, string? Comment)
{
    public bool IsSampleable => Kind is ColumnKind.Text or ColumnKind.Number or ColumnKind.Temporal;
}

/// <summary>A base table with its columns. <see cref="EstimatedRows"/> comes from optimizer statistics, not COUNT(*).</summary>
public sealed record TableInfo(string Schema, string Name, long? EstimatedRows, string? Comment, IReadOnlyList<ColumnInfo> Columns)
{
    public ColumnInfo? Column(string name) =>
        Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Portable column types used to create the demo / benchmark schemas on either engine.</summary>
public enum LogicalType
{
    Integer64,
    Text,
    LongText,
    Date,
    Timestamp,
    Amount,
    Binary,
}

/// <summary>Portable column definition; each dialect renders it to its own DDL.</summary>
public sealed record ColumnSpec(string Name, LogicalType Type, int Length = 100, bool Nullable = true, bool PrimaryKey = false, string? Comment = null);

/// <summary>Portable table definition.</summary>
public sealed record TableSpec(string Name, IReadOnlyList<ColumnSpec> Columns, string? Comment = null);
