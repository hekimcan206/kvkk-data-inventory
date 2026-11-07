using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KvkkInventory.UnitTests.Api;

/// <summary>The API without databases configured: /detect and health work, scans of unknown databases are 404.</summary>
public sealed class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Detect_classifies_a_column_and_explains_why()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/detect",
            new { column = "hasta_tckn", samples = new[] { "10000000146", "12345678950", "98765432150", "10000000146", "12345678950" } }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var top = json.RootElement[0];
        top.GetProperty("type").GetString().Should().Be("NationalId");
        top.GetProperty("category").GetString().Should().Be("Kimlik");
        top.GetProperty("classified").GetBoolean().Should().BeTrue();
        top.GetProperty("reason").GetString().Should().NotContain("10000000146", "explanations never echo values");
    }

    [Fact]
    public async Task Detect_rejects_oversized_and_invalid_requests()
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/detect", new { column = "x", samples = Enumerable.Repeat("a", 1001) }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/detect", new { column = " " }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Scanning_an_unconfigured_database_is_not_found()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/scans", new { database = "oracle", schema = "CLINIC" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("traceId");
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        var client = factory.CreateClient();
        (await client.GetAsync("/health/live", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
