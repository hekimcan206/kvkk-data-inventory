using System.Diagnostics;
using System.Text.Json.Serialization;
using KvkkInventory.Api.Features;
using KvkkInventory.Api.Infrastructure;
using KvkkInventory.Detection;
using KvkkInventory.Inventory;
using KvkkInventory.Oracle;
using KvkkInventory.Postgres;
using KvkkInventory.Schema;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services.AddSingleton(new DetectionEngine());
builder.Services.AddSingleton(_ => InventoryPolicy.Load(
    builder.Configuration["Inventory:PolicyPath"] is { Length: > 0 } policyPath ? policyPath : Path.Combine(AppContext.BaseDirectory, "inventory-policy.json")));

// Databases are optional: without connection strings the API still serves /detect. Connections use the
// read-only scanner accounts; the API never masks or writes.
var databases = new Dictionary<string, IRelationalDatabase>(StringComparer.OrdinalIgnoreCase);
var health = builder.Services.AddHealthChecks();
if (builder.Configuration.GetConnectionString("Postgres") is { Length: > 0 } pg)
{
    var db = new PostgresRelationalDatabase(pg);
    databases["postgres"] = db;
    health.AddAsyncCheck("postgres", async ct => await Ping(() => db.PingAsync(ct)), tags: [HealthEndpoints.ReadyTag]);
}

if (builder.Configuration.GetConnectionString("Oracle") is { Length: > 0 } ora)
{
    var db = new OracleRelationalDatabase(ora);
    databases["oracle"] = db;
    health.AddAsyncCheck("oracle", async ct => await Ping(() => db.PingAsync(ct)), tags: [HealthEndpoints.ReadyTag]);
}

builder.Services.AddSingleton(new DatabaseRegistry(databases));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "KVKK data inventory API";
    document.Info.Version = "v1";
    document.Info.Description = "Finds and classifies personal data in Oracle and PostgreSQL schemas and returns a VERBİS-style inventory. All demo data is synthetic.";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
// Request logging records method, path, status and timing — never bodies, which may contain sampled personal data.
app.UseSerilogRequestLogging(options =>
    options.GetLevel = (context, _, exception) =>
        exception is null && context.Response.StatusCode < 400 && context.Request.Path.StartsWithSegments("/health")
            ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("KVKK data inventory API"));
}

app.MapHealthEndpoints();
app.MapGroup("/api/v1")
    .WithGroupName("v1")
    .MapInventoryEndpoints();

await app.RunAsync();

static async Task<HealthCheckResult> Ping(Func<Task<bool>> ping)
{
    try
    {
        return await ping() ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
    catch (System.Data.Common.DbException ex)
    {
        return HealthCheckResult.Unhealthy("database unavailable", ex);
    }
}

/// <summary>Entry point marker for WebApplicationFactory in tests.</summary>
public partial class Program;
