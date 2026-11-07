using System.ComponentModel.DataAnnotations;
using KvkkInventory.Api.Infrastructure;
using KvkkInventory.Classification;
using KvkkInventory.Detection;
using KvkkInventory.Inventory;
using KvkkInventory.Scanning;
using KvkkInventory.Schema;

namespace KvkkInventory.Api.Features;

/// <summary>Request to classify one column from its name and a handful of values (nothing is stored or logged).</summary>
public sealed record DetectRequest([property: Required] string Column, string? Table, string? Comment, IReadOnlyList<string>? Samples);

public sealed record ScanRequest([property: Required] string Database, [property: Required] string Schema);

/// <summary>Configured databases the API may scan, keyed by name (postgres / oracle). Read-only scanner accounts.</summary>
public sealed class DatabaseRegistry(IReadOnlyDictionary<string, IRelationalDatabase> databases)
{
    public IReadOnlyDictionary<string, IRelationalDatabase> Databases { get; } = databases;
}

internal static class InventoryEndpoints
{
    private const int MaxSamples = 1000;

    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/detect", Detect)
            .WithName("Detect")
            .WithSummary("Classify a single column from its name, comment and sample values.")
            .WithDescription("Stateless: values are evaluated in memory and never stored or logged. Up to 1000 samples.");

        group.MapPost("/scans", ScanAsync)
            .WithName("Scan")
            .WithSummary("Scan a configured database schema and return the KVKK classification.");

        group.MapPost("/inventory", InventoryAsync)
            .WithName("Inventory")
            .WithSummary("Scan a configured schema and return the VERBİS-style inventory (JSON).");

        return group;
    }

    private static IResult Detect(DetectRequest request, DetectionEngine engine)
    {
        if (string.IsNullOrWhiteSpace(request.Column))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["column"] = ["Column name is required."] });
        }

        if (request.Samples is { Count: > MaxSamples })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["samples"] = [$"At most {MaxSamples} samples per request."] });
        }

        var evidence = engine.Detect(new ColumnContext(request.Table ?? "request", request.Column, null, request.Comment, request.Samples ?? []));
        return TypedResults.Ok(evidence.Select(e => new
        {
            e.Type,
            Category = KvkkTaxonomy.CategoryOf(e.Type).Name,
            Special = KvkkTaxonomy.IsSpecial(e.Type),
            e.Confidence,
            Classified = e.Confidence >= 0.5,
            e.Reason,
        }));
    }

    private static async Task<IResult> ScanAsync(ScanRequest request, DatabaseRegistry registry, DetectionEngine engine, ScanMetrics metrics, CancellationToken cancellationToken)
    {
        if (!registry.Databases.TryGetValue(request.Database, out var database))
        {
            return TypedResults.Problem($"Database '{request.Database}' is not configured.", statusCode: StatusCodes.Status404NotFound);
        }

        var scan = await new SchemaScanner(database, engine).ScanAsync(request.Schema, cancellationToken);
        var classification = Classifier.Classify(scan);
        metrics.Record(classification, scan.DurationMs);
        return TypedResults.Ok(classification);
    }

    private static async Task<IResult> InventoryAsync(ScanRequest request, DatabaseRegistry registry, DetectionEngine engine, InventoryPolicy policy, ScanMetrics metrics, CancellationToken cancellationToken)
    {
        if (!registry.Databases.TryGetValue(request.Database, out var database))
        {
            return TypedResults.Problem($"Database '{request.Database}' is not configured.", statusCode: StatusCodes.Status404NotFound);
        }

        var scan = await new SchemaScanner(database, engine).ScanAsync(request.Schema, cancellationToken);
        var classification = Classifier.Classify(scan);
        metrics.Record(classification, scan.DurationMs);
        return TypedResults.Text(InventoryRenderer.Json(InventoryBuilder.Build(classification, policy)), "application/json; charset=utf-8");
    }
}
