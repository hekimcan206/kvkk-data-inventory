using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using KvkkInventory.Schema;
using Npgsql;

namespace KvkkInventory.Postgres;

/// <summary>
/// PostgreSQL implementation. The catalog comes from <c>information_schema.columns</c> joined to
/// <c>pg_catalog.pg_class</c> (row estimate, base tables only) and <c>col_description</c> (comments).
/// <c>information_schema</c> only lists columns the role holds a privilege on, so a least-privilege scanner
/// sees exactly what it was granted — nothing more.
/// </summary>
public sealed class PostgresRelationalDatabase(string connectionString) : IRelationalDatabase, IAsyncDisposable
{
    private const int InsertBatchRows = 500;

    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

    public string Dialect => "postgresql";

    public async Task<IReadOnlyList<TableInfo>> ReadCatalogAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.table_name, c.column_name, c.data_type, c.character_maximum_length, c.is_nullable = 'YES',
                   c.ordinal_position, col_description(cl.oid, c.ordinal_position::int), cl.reltuples::bigint,
                   obj_description(cl.oid, 'pg_class')
            FROM information_schema.columns c
            JOIN pg_catalog.pg_namespace n ON n.nspname = c.table_schema
            JOIN pg_catalog.pg_class cl ON cl.relnamespace = n.oid AND cl.relname = c.table_name
            WHERE c.table_schema = @schema AND cl.relkind IN ('r', 'p')
            ORDER BY c.table_name, c.ordinal_position
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("schema", schema);
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
                (current, rows, tableComment) = (table, reader.GetInt64(7) is var r && r >= 0 ? r : null, reader.IsDBNull(8) ? null : reader.GetString(8));
            }

            var dataType = reader.GetString(2);
            columns.Add(new ColumnInfo(
                reader.GetString(1),
                dataType,
                KindOf(dataType),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetBoolean(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        Flush();
        return tables;

        void Flush()
        {
            if (current is not null)
            {
                tables.Add(new TableInfo(schema, current, rows, tableComment, [.. columns]));
                columns.Clear();
            }
        }
    }

    public async Task<IReadOnlyList<object?[]>> SampleAsync(TableInfo table, IReadOnlyList<ColumnInfo> columns, int rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);
        var list = string.Join(", ", columns.Select(c => Quote(c.Name)));

        // Large tables: a Bernoulli sample over the whole table instead of the first N rows (which are usually the
        // oldest, most uniform rows). Small tables: read them, sampling would only lose rows.
        var sample = table.EstimatedRows is { } estimate && estimate > rows * 10L
            ? string.Create(CultureInfo.InvariantCulture, $" TABLESAMPLE BERNOULLI ({Math.Min(100, 300.0 * rows / estimate):0.######})")
            : string.Empty;

        await using var command = _dataSource.CreateCommand($"SELECT {list} FROM {Qualified(table.Schema, table.Name)}{sample} LIMIT @rows");
        command.Parameters.AddWithValue("rows", rows);
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
        await using var command = _dataSource.CreateCommand($"SELECT {list} FROM {Qualified(table.Schema, table.Name)}");
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

    public Task DropTableIfExistsAsync(string schema, string table, CancellationToken cancellationToken = default) =>
        ExecuteAsync($"DROP TABLE IF EXISTS {Qualified(schema, table)}", cancellationToken);

    public async Task CreateEmptyCopyAsync(TableInfo source, string targetSchema, IReadOnlyCollection<string> makeNullable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(makeNullable);
        // LIKE copies column types, NOT NULL and comments, but no data, constraints to other tables or triggers.
        await ExecuteAsync(
            $"CREATE TABLE {Qualified(targetSchema, source.Name)} (LIKE {Qualified(source.Schema, source.Name)} INCLUDING DEFAULTS INCLUDING COMMENTS)",
            cancellationToken);
        foreach (var column in makeNullable)
        {
            await ExecuteAsync($"ALTER TABLE {Qualified(targetSchema, source.Name)} ALTER COLUMN {Quote(column)} DROP NOT NULL", cancellationToken);
        }
    }

    public async Task<long> InsertAsync(string schema, string table, IReadOnlyList<string> columns, IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        var head = $"INSERT INTO {Qualified(schema, table)} ({string.Join(", ", columns.Select(Quote))}) VALUES ";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var batch = new List<object?[]>(InsertBatchRows);
        long total = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            batch.Add(row);
            if (batch.Count == InsertBatchRows)
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

            // One multi-row INSERT per batch: one round trip, values always bound as parameters.
            await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };
            var sql = new StringBuilder(head);
            for (var r = 0; r < batch.Count; r++)
            {
                sql.Append(r == 0 ? "(" : ", (");
                for (var c = 0; c < columns.Count; c++)
                {
                    var name = string.Create(CultureInfo.InvariantCulture, $"p{r}_{c}");
                    sql.Append(c == 0 ? "@" : ", @").Append(name);
                    command.Parameters.Add(new NpgsqlParameter(name, batch[r][c] ?? DBNull.Value));
                }

                sql.Append(')');
            }

            command.CommandText = sql.ToString();
            var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
            batch.Clear();
            return inserted;
        }
    }

    public Task GrantSelectAsync(string schema, string table, string grantee, CancellationToken cancellationToken = default) =>
        ExecuteAsync($"GRANT SELECT ON {Qualified(schema, table)} TO {Quote(grantee)}", cancellationToken);

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand("SELECT 1");
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    /// <summary>Double-quoted identifier with embedded quotes doubled: never injectable, whatever the catalog returns.</summary>
    public static string Quote(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string Qualified(string schema, string table) => Quote(schema) + "." + Quote(table);

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static object?[] ReadRow(NpgsqlDataReader reader)
    {
        var row = new object?[reader.FieldCount];
        for (var i = 0; i < row.Length; i++)
        {
            row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static ColumnKind KindOf(string dataType) => dataType switch
    {
        "character varying" or "character" or "text" or "citext" => ColumnKind.Text,
        "smallint" or "integer" or "bigint" or "numeric" or "real" or "double precision" => ColumnKind.Number,
        "date" or "timestamp without time zone" or "timestamp with time zone" => ColumnKind.Temporal,
        "bytea" => ColumnKind.Binary,
        _ => ColumnKind.Other,
    };

    private static string TypeOf(ColumnSpec column) => column.Type switch
    {
        LogicalType.Integer64 => "bigint",
        LogicalType.Text => string.Create(CultureInfo.InvariantCulture, $"varchar({column.Length})"),
        LogicalType.LongText => "text",
        LogicalType.Date => "date",
        LogicalType.Timestamp => "timestamp",
        LogicalType.Amount => "numeric(14,2)",
        LogicalType.Binary => "bytea",
        _ => throw new ArgumentOutOfRangeException(nameof(column), column.Type, null),
    };
}
