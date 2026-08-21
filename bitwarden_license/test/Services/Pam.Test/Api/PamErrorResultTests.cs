using System.Text.Json;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Services.Pam.Api;
using Bit.Services.Pam.Errors;
using Bit.Services.Pam.Test.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Api;

/// <summary>
/// The wire contract every PAM client reads. These assert the bytes rather than the result type, because the code
/// and its placement in the body are the promise — an SDK switches on <c>errors.&lt;property&gt;[].type</c>, and a
/// change to that shape is a breaking change however the C# side is spelled.
/// </summary>
public class PamErrorResultTests
{
    [Fact]
    public async Task ExecuteAsync_ValidationError_Writes400ProblemCarryingTheCode()
    {
        var (status, contentType, body) = await Execute(new ReasonRequired());

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal("validation_error", body.GetProperty("type").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());

        var entry = body.GetProperty("errors").GetProperty("reason").EnumerateArray().Single();
        Assert.Equal("reason_required", entry.GetProperty("type").GetString());
        Assert.Equal("A reason is required for items that need human approval.", entry.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ConflictError_Writes409WithTheSameBodyShape()
    {
        var (status, contentType, body) = await Execute(new AccessAlreadyActive());

        // A state conflict, not bad input — but the body a client parses is identical, so one parser handles both.
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal("conflict_error", body.GetProperty("type").GetString());
        Assert.Equal(409, body.GetProperty("status").GetInt32());

        var entry = body.GetProperty("errors").GetProperty("code").EnumerateArray().Single();
        Assert.Equal("access_already_active", entry.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ErrorNamingAProperty_KeysTheBodyByThatProperty()
    {
        var (_, _, body) = await Execute(new AccessRuleCollectionsAlreadyGoverned());

        var errors = body.GetProperty("errors");
        Assert.False(errors.TryGetProperty("code", out _));
        Assert.Equal(
            "collections_already_governed",
            errors.GetProperty("collections").EnumerateArray().Single().GetProperty("type").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_DurationExceedsMax_CarriesTheBoundInTheDetail()
    {
        var (_, _, body) = await Execute(new DurationExceedsMax(900));

        var entry = body.GetProperty("errors").GetProperty("durationSeconds").EnumerateArray().Single();
        Assert.Equal("duration_exceeds_max", entry.GetProperty("type").GetString());
        Assert.Contains("900", entry.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_KeepsTheEnvelopeAThrownNotFoundProduces()
    {
        // Deliberately not a problem response: a 404 needs no code, since the status already tells it apart, and
        // PAM's other 404s still come from the shared exception filter in this shape.
        var (status, contentType, body) = await Execute(new AccessRuleNotFound());

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.StartsWith("application/json", contentType);
        Assert.Equal("error", body.GetProperty("object").GetString());
        Assert.Equal("Resource not found.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_UncodedBadRequest_FallsBackToTheErrorEnvelope()
    {
        var (status, _, body) = await Execute(new UncodedFailure());

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("Something went wrong.", body.GetProperty("message").GetString());
    }

    private sealed record UncodedFailure() : BadRequestError("Something went wrong.");

    private static async Task<(int Status, string ContentType, JsonElement Body)> Execute(Error error)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider(),
        };
        var body = new MemoryStream();
        context.Response.Body = body;

        await PamErrorResult.From(error).ExecuteAsync(context);

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        return (context.Response.StatusCode, context.Response.ContentType ?? string.Empty, document.RootElement.Clone());
    }

    [Fact]
    public void ValidationErrors_AreAlsoOneOfTheStatusCarryingErrorKinds()
    {
        // PamErrorResult reads the status off the error's base type, so a coded error that inherits neither
        // BadRequestError nor ConflictError would silently render as a 500.
        foreach (var type in PamErrorCatalog.CodedErrors())
        {
            Assert.True(
                typeof(BadRequestError).IsAssignableFrom(type) || typeof(ConflictError).IsAssignableFrom(type),
                $"{type.Name} implements IValidationError but is neither a BadRequestError nor a ConflictError.");
        }
    }
}
