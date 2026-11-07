using DotNet.Testcontainers.Configurations;
using KvkkInventory.Oracle;
using KvkkInventory.Postgres;
using KvkkInventory.Schema;
using KvkkInventory.Synthetic;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(KvkkInventory.IntegrationTests.PostgresFixture))]
[assembly: AssemblyFixture(typeof(KvkkInventory.IntegrationTests.OracleFixture))]

namespace KvkkInventory.IntegrationTests;

/// <summary>One engine with the three accounts created by the same init script docker compose uses.</summary>
public interface IDatabaseFixture
{
    string Dialect { get; }

    /// <summary>Owner of the source schema (DDL + data); used only to set the scene.</summary>
    IRelationalDatabase Owner { get; }

    /// <summary>Read-only scanner account.</summary>
    IRelationalDatabase Scanner { get; }

    /// <summary>Masking account: reads the source, writes its own target schema.</summary>
    IRelationalDatabase Masker { get; }

    string SourceSchema { get; }

    string TargetSchema { get; }

    Task ExecuteAsync(IRelationalDatabase database, string sql);
}

internal static class FixtureDefaults
{
    public const int Rows = 300;

    // Test-only credentials for throwaway containers.
    public const string OwnerPassword = "OwnerTest1";
    public const string ScannerPassword = "ScannerTest1";
    public const string MaskerPassword = "MaskerTest1";

    public static byte[] InitScript(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "db-init", name));

    public const UnixFileModes Executable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute
        | UnixFileModes.GroupRead | UnixFileModes.GroupExecute | UnixFileModes.OtherRead | UnixFileModes.OtherExecute;

    /// <summary>Seeds the labelled schema and a table the scanner is deliberately NOT granted.</summary>
    public static async Task SeedAsync(IDatabaseFixture fixture, string secretTableSql)
    {
        await new SyntheticDataLoader(fixture.Owner).LoadAsync(fixture.SourceSchema, ClinicSchema.Tables(), Rows, grantSelectTo: "kvkk_read");
        await fixture.ExecuteAsync(fixture.Owner, secretTableSql);
    }
}

public sealed class PostgresFixture : IDatabaseFixture, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("kvkk")
        .WithEnvironment("OWNER_PASSWORD", FixtureDefaults.OwnerPassword)
        .WithEnvironment("SCANNER_PASSWORD", FixtureDefaults.ScannerPassword)
        .WithEnvironment("MASKER_PASSWORD", FixtureDefaults.MaskerPassword)
        .WithResourceMapping(FixtureDefaults.InitScript("postgres-01-roles.sh"), "/docker-entrypoint-initdb.d/01-roles.sh", fileMode: FixtureDefaults.Executable)
        .WithLabel("project", "kvkk-data-inventory-tests")
        .Build();

    private PostgresRelationalDatabase? _owner, _scanner, _masker;

    public string Dialect => "postgresql";

    public IRelationalDatabase Owner => _owner!;

    public IRelationalDatabase Scanner => _scanner!;

    public IRelationalDatabase Masker => _masker!;

    public string SourceSchema => "clinic";

    public string TargetSchema => "clinic_masked";

    public string ConnectionString(string user, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Username = user, Password = password, Timeout = 60 }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _owner = new PostgresRelationalDatabase(ConnectionString("clinic_owner", FixtureDefaults.OwnerPassword));
        _scanner = new PostgresRelationalDatabase(ConnectionString("kvkk_scanner", FixtureDefaults.ScannerPassword));
        _masker = new PostgresRelationalDatabase(ConnectionString("kvkk_masker", FixtureDefaults.MaskerPassword));
        await FixtureDefaults.SeedAsync(this, "CREATE TABLE clinic.secret_notes (id bigint, tckn varchar(11))");
    }

    public Task ExecuteAsync(IRelationalDatabase database, string sql) => ((PostgresRelationalDatabase)database).ExecuteAsync(sql);

    public async ValueTask DisposeAsync()
    {
        foreach (var db in new[] { _owner, _scanner, _masker })
        {
            if (db is not null)
            {
                await db.DisposeAsync();
            }
        }

        await _container.DisposeAsync();
    }
}

public sealed class OracleFixture : IDatabaseFixture, IAsyncLifetime
{
    private readonly OracleContainer _container = new OracleBuilder("gvenzl/oracle-free:23-slim-faststart")
        .WithEnvironment("OWNER_PASSWORD", FixtureDefaults.OwnerPassword)
        .WithEnvironment("SCANNER_PASSWORD", FixtureDefaults.ScannerPassword)
        .WithEnvironment("MASKER_PASSWORD", FixtureDefaults.MaskerPassword)
        .WithResourceMapping(FixtureDefaults.InitScript("oracle-01-users.sh"), "/container-entrypoint-initdb.d/01-users.sh", fileMode: FixtureDefaults.Executable)
        .WithLabel("project", "kvkk-data-inventory-tests")
        .Build();

    private OracleRelationalDatabase? _owner, _scanner, _masker;

    public string Dialect => "oracle";

    public IRelationalDatabase Owner => _owner!;

    public IRelationalDatabase Scanner => _scanner!;

    public IRelationalDatabase Masker => _masker!;

    public string SourceSchema => "CLINIC";

    public string TargetSchema => "KVKK_MASKED";

    public string ConnectionString(string user, string password) =>
        new OracleConnectionStringBuilder(_container.GetConnectionString()) { UserID = user, Password = password, ConnectionTimeout = 60 }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _owner = new OracleRelationalDatabase(ConnectionString("clinic", FixtureDefaults.OwnerPassword));
        _scanner = new OracleRelationalDatabase(ConnectionString("kvkk_scanner", FixtureDefaults.ScannerPassword));
        _masker = new OracleRelationalDatabase(ConnectionString("kvkk_masked", FixtureDefaults.MaskerPassword));
        await FixtureDefaults.SeedAsync(this, "CREATE TABLE secret_notes (id NUMBER(19), tckn VARCHAR2(11))");
    }

    public Task ExecuteAsync(IRelationalDatabase database, string sql) => ((OracleRelationalDatabase)database).ExecuteAsync(sql);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
