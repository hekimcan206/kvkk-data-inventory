using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using KvkkInventory.Schema;
using Oracle.ManagedDataAccess.Client;

namespace KvkkInventory.Oracle;

/// <summary>
/// Oracle implementation (tested on Oracle Database 23ai Free). The catalog comes from <c>ALL_TAB_COLUMNS</c> joined
/// to <c>ALL_TABLES</c> (base tables, optimizer row estimate) and <c>ALL_COL_COMMENTS</c>. The <c>ALL_*</c> views
/// only show objects the session can access, so the scanner account's grants define the scan scope.
/// A schema is an Oracle user; names are stored upper-case as Oracle does for unquoted identifiers.
/// </summary>
public sealed class OracleRelationalDatabase(string connectionString) : IRelationalDatabase
{
    private const int ArrayBindRows = 5_000;

    public string Dialect => "oracle";

    public async Task<IReadOnlyList<TableInfo>> ReadCatalogAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.table_name, c.column_name, c.data_type, c.char_length, c.nullable, c.column_id, cc.comments,
                   t.num_rows, tc.comments
            FROM all_tab_columns c
            JOIN all_tables t ON t.owner = c.owner AND t.table_name = c.table_name
            LEFT JOIN all_col_comments cc ON cc.owner = c.owner AND cc.table_name = c.table_name AND cc.column_name = c.column_name
            LEFT JOIN all_tab_comments tc ON tc.owner = c.owner AND tc.table_name = c.table_name
            WHERE c.owner = :owner
            ORDER BY c.table_name, c.column_id
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(new OracleParameter("owner", schema.ToUpperInvariant()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new List<TableInfo>();
        string? current = null;
        long? rows = null;
        string? tableComment = null;
        var columns = new List<ColumnInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            if (table != current)
            {
                Flush();
                current = table;
                rows = reader.IsDBNull(7) ? null : Convert.ToInt64(reader.GetDecimal(7));
                tableComment = reader.IsDBNull(8) ? null : reader.GetString(8);
            }

            var dataType = reader.GetString(2);
            var kind = KindOf(dataType);
            var charLength = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetDecimal(3));
            columns.Add(new ColumnInfo(
                reader.GetString(1),
                dataType,
                kind,
                kind == ColumnKind.Text && charLength > 0 ? charLength : null,
                reader.GetString(4) == "Y",
                Convert.ToInt32(reader.GetDecimal(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        Flush();
        return tables;

        void Flush()
        {
            if (current is not null)
            {
                tables.Add(new TableInfo(schema.ToUpperInvariant(), current, rows, tableComment, [.. columns]));
                columns.Clear();
            }
        }
    }

    public async Task<IReadOnlyList<object?[]>> SampleAsync(TableInfo table, IReadOnlyList<ColumnInfo> columns, int rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);
        var list = string.Join(", ", columns.Select(c => Quote(c.Name)));

        // SAMPLE (p) reads a random p % of rows on large tables; FETCH FIRST bounds the result either way.
        var sample = table.EstimatedRows is { } estimate && estimate > rows * 10L
            ? string.Create(CultureInfo.InvariantCulture, $" SAMPLE ({Math.Min(99.0, 300.0 * rows / estimate):0.######})")
            : string.Empty;
        var sql = string.Create(CultureInfo.InvariantCulture, $"SELECT {list} FROM {Qualified(table.Schema, table.Name)}{sample} FETCH FIRST {rows} ROWS ONLY");

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<object?[]>(rows);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadRow(reader));
        }

        return result;
    }

    public async IAsyncEnumerable<object?[]> ReadAllAsync(TableInfo table, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        var list = string.Join(", ", table.Columns.Select(c => Quote(c.Name)));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new OracleCommand($"SELECT {list} FROM {Qualified(table.Schema, table.Name)}", connection) { FetchSize = 1024 * 1024 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return ReadRow(reader);
        }
    }

    public async Task CreateTableAsync(string schema, TableSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var ddl = new StringBuilder($"CREATE TABLE {Qualified(schema, spec.Name)} (");
        ddl.AppendJoin(", ", spec.Columns.Select(c =>
            $"{Quote(c.Name)} {TypeOf(c)}{(c.PrimaryKey ? " PRIMARY KEY" : c.Nullable ? string.Empty : " NOT NULL")}"));
        ddl.Append(')');
        await ExecuteAsync(ddl.ToString(), cancellationToken);

        if (spec.Comment is not null)
        {
            await ExecuteAsync($"COMMENT ON TABLE {Qualified(schema, spec.Name)} IS {Literal(spec.Comment)}", cancellationToken);
        }

        foreach (var column in spec.Columns.Where(c => c.Comment is not null))
        {
            await ExecuteAsync($"COMMENT ON COLUMN {Qualified(schema, spec.Name)}.{Quote(column.Name)} IS {Literal(column.Comment!)}", cancellationToken);
        }
    }

    // DROP TABLE IF EXISTS is available from Oracle Database 23ai.
    public Task DropTableIfExistsAsync(string schema, string table, CancellationToken cancellationToken = default) =>
        ExecuteAsync($"DROP TABLE IF EXISTS {Qualified(schema, table)} PURGE", cancellationToken);

    /// <summary>
    /// CTAS with an always-false predicate copies column definitions and NOT NULL constraints, no rows. The target
    /// schema should be the connected (masking) account itself, so it needs CREATE TABLE in its own schema only,
    /// never CREATE ANY TABLE.
    /// </summary>
    public async Task CreateEmptyCopyAsync(TableInfo source, string targetSchema, IReadOnlyCollection<string> makeNullable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(makeNullable);
        await ExecuteAsync($"CREATE TABLE {Qualified(targetSchema, source.Name)} AS SELECT * FROM {Qualified(source.Schema, source.Name)} WHERE 1 = 0", cancellationToken);
        foreach (var column in makeNullable)
        {
            if (source.Column(column) is { Nullable: false } notNull)
            {
                await ExecuteAsync($"ALTER TABLE {Qualified(targetSchema, source.Name)} MODIFY ({Quote(notNull.Name)} NULL)", cancellationToken);
            }
        }
    }

    /// <summary>ODP.NET array binding: one INSERT, one round trip per <see cref="ArrayBindRows"/> rows.</summary>
    public async Task<long> InsertAsync(string schema, string table, IReadOnlyList<string> columns, IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        var sql = $"INSERT INTO {Qualified(schema, table)} ({string.Join(", ", columns.Select(Quote))}) VALUES ({string.Join(", ", columns.Select((_, i) => ":" + (i + 1).ToString(CultureInfo.InvariantCulture)))})";

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (OracleTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var batch = new List<object?[]>(ArrayBindRows);
        long total = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            batch.Add(row);
            if (batch.Count == ArrayBindRows)
            {
                total += await FlushAsync();
            }
        }

        total += await FlushAsync();
        await transaction.CommitAsync(cancellationToken);
        return total;

        async Task<int> FlushAsync()
        {
            if (batch.Count == 0)
            {
                return 0;
            }

            await using var command = new OracleCommand(sql, connection) { ArrayBindCount = batch.Count, Transaction = transaction };
            for (var c = 0; c < columns.Count; c++)
            {
                var values = new object[batch.Count];
                Type? clr = null;
                for (var r = 0; r < batch.Count; r++)
                {
                    var value = batch[r][c];
                    values[r] = value ?? DBNull.Value;
                    clr ??= value?.GetType();
                }

                command.Parameters.Add(new OracleParameter { OracleDbType = DbTypeOf(clr), Value = values });
            }

            var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
            batch.Clear();
            return inserted;
        }
    }

    public Task GrantSelectAsync(string schema, string table, string grantee, CancellationToken cancellationToken = default) =>
        ExecuteAsync($"GRANT SELECT ON {Qualified(schema, table)} TO {Quote(grantee.ToUpperInvariant())}", cancellationToken);

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new OracleCommand("SELECT 1 FROM dual", connection);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    /// <summary>Quoted, upper-cased identifier; embedded quotes doubled.</summary>
    public static string Quote(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "\"" + identifier.ToUpperInvariant().Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private async Task<OracleConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new OracleConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static string Qualified(string schema, string table) => Quote(schema) + "." + Quote(table);

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static object?[] ReadRow(OracleDataReader reader)
    {
        var row = new object?[reader.FieldCount];
        for (var i = 0; i < row.Length; i++)
        {
            row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static OracleDbType DbTypeOf(Type? clr) => clr switch
    {
        null => OracleDbType.Varchar2,
        _ when clr == typeof(string) => OracleDbType.Varchar2,
        _ when clr == typeof(decimal) => OracleDbType.Decimal,
        _ when clr == typeof(long) => OracleDbType.Int64,
        _ when clr == typeof(int) => OracleDbType.Int32,
        _ when clr == typeof(double) => OracleDbType.Double,
        _ when clr == typeof(DateTime) => OracleDbType.TimeStamp,
        // RAW binds convert implicitly to BLOB columns; fine for the small binary values masking produces.
        _ when clr == typeof(byte[]) => OracleDbType.Raw,
        _ => OracleDbType.Varchar2,
    };

    private static ColumnKind KindOf(string dataType) => dataType switch
    {
        "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR" => ColumnKind.Text,
        "NUMBER" or "FLOAT" or "BINARY_FLOAT" or "BINARY_DOUBLE" => ColumnKind.Number,
        "DATE" => ColumnKind.Temporal,
        _ when dataType.StartsWith("TIMESTAMP", StringComparison.Ordinal) => ColumnKind.Temporal,
        "BLOB" or "RAW" or "LONG RAW" or "BFILE" => ColumnKind.Binary,
        "CLOB" or "NCLOB" or "LONG" => ColumnKind.LargeText,
        _ => ColumnKind.Other,
    };

    private static string TypeOf(ColumnSpec column) => column.Type switch
    {
        LogicalType.Integer64 => "NUMBER(19)",
        LogicalType.Text => string.Create(CultureInfo.InvariantCulture, $"VARCHAR2({column.Length} CHAR)"),
        LogicalType.LongText => "VARCHAR2(4000 CHAR)",
        LogicalType.Date => "DATE",
        LogicalType.Timestamp => "TIMESTAMP",
        LogicalType.Amount => "NUMBER(14,2)",
        LogicalType.Binary => "BLOB",
        _ => throw new ArgumentOutOfRangeException(nameof(column), column.Type, null),
    };
}
