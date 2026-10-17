using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Controllers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipErrorEnvelopeTests : IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public PartnershipErrorEnvelopeTests(ApiApplicationFactory factory)
    {
        factory.ConfigureServices(services =>
            services.AddMvcCore().AddApplicationPart(typeof(PartnershipEnvelopeTestController).Assembly));
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Success_HasRequestIdHeader()
    {
        var response = await _client.GetAsync("/partnerships/v1/envelope-test/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(RequestIdHeader(response)));
    }

    [Fact]
    public async Task DomainError_ReturnsEnvelopeWithMatchingRequestId()
    {
        var response = await _client.GetAsync("/partnerships/v1/envelope-test/conflict");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(["object", "code", "message", "requestId"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("error", body.GetProperty("object").GetString());
        Assert.Equal("partnership_already_exists", body.GetProperty("code").GetString());
        Assert.Equal(new PartnershipAlreadyExists().Message, body.GetProperty("message").GetString());
        Assert.Equal(RequestIdHeader(response), body.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task UnhandledException_Returns500EnvelopeWithoutExceptionText()
    {
        var response = await _client.GetAsync("/partnerships/v1/envelope-test/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(PartnershipEnvelopeTestController.ExceptionText, raw);
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("internal_error", body.GetProperty("code").GetString());
        Assert.Equal(RequestIdHeader(response), body.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task InvalidModel_Returns400Envelope()
    {
        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/partnerships/v1/envelope-test/model", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("invalid_request", body.GetProperty("code").GetString());
        Assert.Equal(RequestIdHeader(response), body.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task NonPartnershipPath_HasNoRequestIdHeader()
    {
        var response = await _client.GetAsync("/alive");

        Assert.False(response.Headers.Contains("X-Request-Id"));
    }

    private static string? RequestIdHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-Request-Id", out var values) ? values.Single() : null;

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}

public class PartnershipEnvelopeTestRequestModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string? Name { get; set; }
}

[Route("partnerships/v1/envelope-test")]
public class PartnershipEnvelopeTestController : PartnershipControllerBase
{
    public const string ExceptionText = "Duplicate key value is (ext-123).";

    [HttpGet("ok")]
    public ActionResult<string> GetOk() => "ok";

    [HttpGet("conflict")]
    public ActionResult<string> GetConflict() => PartnershipError(new PartnershipAlreadyExists());

    [HttpGet("throw")]
    public ActionResult<string> GetThrow() => throw new InvalidOperationException(ExceptionText);

    [HttpPost("model")]
    public ActionResult<string> PostModel([FromBody] PartnershipEnvelopeTestRequestModel model) => "ok";
}
