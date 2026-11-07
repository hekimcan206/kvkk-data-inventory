namespace KvkkInventory.Schema;

/// <summary>
/// The one abstraction over Oracle and PostgreSQL. Everything above it (detection, classification, masking,
/// inventory) is dialect-free; everything below it is catalog SQL, sampling SQL, DDL and bulk insert for one engine.
/// Identifiers that reach SQL text come from the catalog or from a validated spec and are always quoted;
/// values are always bound as parameters.
/// </summary>
public interface IRelationalDatabase
{
    /// <summary><c>postgresql</c>, <c>oracle</c> or <c>memory</c>.</summary>
    string Dialect { get; }

    /// <summary>Base tables and columns of a schema that the connected account is allowed to see.</summary>
    Task<IReadOnlyList<TableInfo>> ReadCatalogAsync(string schema, CancellationToken cancellationToken = default);

    /// <summary>Up to <paramref name="rows"/> rows of the given columns, sampled (not just the first rows) on large tables.</summary>
    Task<IReadOnlyList<object?[]>> SampleAsync(TableInfo table, IReadOnlyList<ColumnInfo> columns, int rows, CancellationToken cancellationToken = default);

    /// <summary>Streams every row of the table, all columns in catalog order.</summary>
    IAsyncEnumerable<object?[]> ReadAllAsync(TableInfo table, CancellationToken cancellationToken = default);

    Task CreateTableAsync(string schema, TableSpec spec, CancellationToken cancellationToken = default);

    Task DropTableIfExistsAsync(string schema, string table, CancellationToken cancellationToken = default);

    /// <summary>Creates an empty table in <paramref name="targetSchema"/> with the source's columns; listed columns become nullable.</summary>
    Task CreateEmptyCopyAsync(TableInfo source, string targetSchema, IReadOnlyCollection<string> makeNullable, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(string schema, string table, IReadOnlyList<string> columns, IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken = default);

    Task GrantSelectAsync(string schema, string table, string grantee, CancellationToken cancellationToken = default);
}

public static class RelationalDatabaseExtensions
{
    public static Task<long> InsertAsync(this IRelationalDatabase database, string schema, string table, IReadOnlyList<string> columns,
        IEnumerable<object?[]> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.InsertAsync(schema, table, columns, ToAsync(rows), cancellationToken);
    }

    private static async IAsyncEnumerable<object?[]> ToAsync(IEnumerable<object?[]> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }
}
