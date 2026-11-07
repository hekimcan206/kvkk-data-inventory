using System.Globalization;
using KvkkInventory.Oracle;
using KvkkInventory.Postgres;
using KvkkInventory.Schema;

namespace KvkkInventory.Cli;

internal static class Databases
{
    /// <summary>Published, deliberately weak-in-practice key for the in-memory demo only. Never use it for real data.</summary>
    public const string DemoKey = "ZGVtby1rZXktZG8tbm90LXVzZS1mb3ItcmVhbC1kYXRhIQ==";

    public static IRelationalDatabase Open(Options o)
    {
        var dialect = o.Require("db");
        if (dialect == "memory")
        {
            throw new ArgumentException("--db memory is only available through `kvkk demo`.");
        }

        var variable = o.Get("conn-env") ?? "KVKK_CONNECTION";
        var connection = Environment.GetEnvironmentVariable(variable)
            ?? throw new ArgumentException($"Set the {variable} environment variable to the connection string.");
        return dialect switch
        {
            "postgres" or "postgresql" => new PostgresRelationalDatabase(connection),
            "oracle" => new OracleRelationalDatabase(connection),
            _ => throw new ArgumentException($"Unknown --db '{dialect}' (postgres | oracle)."),
        };
    }
}

/// <summary>Minimal <c>--name value</c> / <c>--flag</c> parser; the CLI surface is small enough not to need a framework.</summary>
internal sealed class Options
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            }

            var name = args[i][2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            options._values[name] = hasValue ? args[++i] : null;
        }

        return options;
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public string? Get(string name) => _values.GetValueOrDefault(name);

    public string Require(string name) => Get(name) ?? throw new ArgumentException($"--{name} is required.");

    public int Int(string name, int fallback) =>
        Get(name) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;

    public double Double(string name, double fallback) =>
        Get(name) is { } value ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;
}
