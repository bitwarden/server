using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Controllers;
using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipIdempotencyTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string Path = "/partnerships/v1/idempotency-test";

    private readonly ApiApplicationFactory _factory;
    private HttpClient _client = null!;

    public PartnershipIdempotencyTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.ConfigureServices(services =>
            services.AddMvcCore().AddApplicationPart(typeof(PartnershipIdempotencyTestController).Assembly));
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    public async Task InitializeAsync() =>
        _client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, PartnershipStatus.Active)).Client;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SameKeyAndBody_ReplaysTheFirstResponse()
    {
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(key, "a");
        var replay = await PostAsync(key, "a");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("application/json; charset=utf-8", replay.Content.Headers.ContentType?.ToString());
        Assert.False(first.Headers.Contains(PartnershipIdempotencyFilter.ReplayedHeaderName));
        Assert.Equal("true", replay.Headers.GetValues(PartnershipIdempotencyFilter.ReplayedHeaderName).Single());
        Assert.True(replay.Headers.Contains("X-Request-Id"));
    }

    [Fact]
    public async Task SameKeyDifferentBody_Returns409Envelope()
    {
        var key = Guid.NewGuid().ToString();
        await PostAsync(key, "a");

        var response = await PostAsync(key, "b");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("error", body.GetProperty("object").GetString());
        Assert.Equal("idempotency_key_reused", body.GetProperty("code").GetString());
        Assert.Equal(response.Headers.GetValues("X-Request-Id").Single(), body.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task NoKey_RunsTheActionEachTime()
    {
        var first = await PostAsync(null, "a");
        var second = await PostAsync(null, "a");

        Assert.NotEqual(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InvalidKey_Returns400Envelope()
    {
        var response = await PostAsync(new string('k', 256), "a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("invalid_idempotency_key", body.GetProperty("code").GetString());
    }

    private Task<HttpResponseMessage> PostAsync(string? key, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { name }), Encoding.UTF8, "application/json"),
        };
        if (key is not null)
        {
            request.Headers.Add(PartnershipIdempotencyFilter.HeaderName, key);
        }

        return _client.SendAsync(request);
    }
}

public class PartnershipIdempotencyTestRequestModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string? Name { get; set; }
}

public record PartnershipIdempotencyTestResponseModel(string Name, Guid ExecutionId);

[Route("partnerships/v1/idempotency-test")]
public class PartnershipIdempotencyTestController : PartnershipControllerBase
{
    [HttpPost, PartnershipIdempotent]
    public ActionResult<PartnershipIdempotencyTestResponseModel> Post(
        [FromBody] PartnershipIdempotencyTestRequestModel model) =>
        StatusCode(StatusCodes.Status201Created, new PartnershipIdempotencyTestResponseModel(model.Name!, Guid.NewGuid()));
}
