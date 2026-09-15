using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.HttpExtensions.RoundTrip.Test;

/// <summary>
/// The whole chain, end to end: a DataAnnotations attribute, the message the framework records for it, the
/// generated map, and the coded document that reaches the client.
/// </summary>
/// <remarks>
/// This is the test that earns the design. The generator recovers a code by recognising what the framework said,
/// so a framework release that rewords a message breaks the mapping silently — everywhere except here, where the
/// assertion is on the code rather than the message and turns the drift into a failure on the next SDK bump.
/// </remarks>
public class ValidationRoundTripTests
{
    [Fact]
    public async Task ARequiredValue_ReportsRequired()
    {
        var errors = await PostAsync(new { });

        Assert.Equal(ValidationCodes.Required, Code(errors, "reason"));
    }

    [Fact]
    public async Task AValueOverItsMaximum_ReportsTooLongAndCarriesTheLimit()
    {
        var errors = await PostAsync(new { reason = "r", name = new string('n', 201) });

        Assert.Equal(ValidationCodes.TooLong, Code(errors, "name"));
        Assert.Equal(200, errors.GetProperty("name")[0].GetProperty("parameters").GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task ANumberOutsideItsRange_ReportsOutOfRangeAndCarriesBothBounds()
    {
        var errors = await PostAsync(new { reason = "r", seats = 0 });

        var parameters = errors.GetProperty("seats")[0].GetProperty("parameters");
        Assert.Equal(ValidationCodes.OutOfRange, Code(errors, "seats"));
        Assert.Equal(1, parameters.GetProperty("min").GetInt32());
        Assert.Equal(100, parameters.GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task AMalformedAddress_ReportsInvalidEmail()
    {
        var errors = await PostAsync(new { reason = "r", email = "not-an-address" });

        Assert.Equal(ValidationCodes.InvalidEmail, Code(errors, "email"));
    }

    [Fact]
    public async Task ATwoEndedLengthConstraint_ReportsOneCodeCarryingBothBounds()
    {
        // The constraint reports the same message whichever end was breached, so both breaches answer alike.
        var tooShort = await PostAsync(new { reason = "r", bounded = "ab" });
        var tooLong = await PostAsync(new { reason = "r", bounded = new string('b', 201) });

        Assert.Equal(ValidationCodes.InvalidLength, Code(tooShort, "bounded"));
        Assert.Equal(ValidationCodes.InvalidLength, Code(tooLong, "bounded"));

        var parameters = tooShort.GetProperty("bounded")[0].GetProperty("parameters");
        Assert.Equal(5, parameters.GetProperty("min").GetInt32());
        Assert.Equal(200, parameters.GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task APropertyThatCanFailTwoWays_ReportsTheOneThatFired()
    {
        // Two attributes on one property, told apart by the message the framework recorded.
        var missing = await PostAsync(new { reason = "r" });
        var overlong = await PostAsync(new { reason = "r", accessCode = new string('a', 26) });

        Assert.Equal(ValidationCodes.Required, Code(missing, "accessCode"));
        Assert.Equal(ValidationCodes.TooLong, Code(overlong, "accessCode"));
        Assert.Equal(25, overlong.GetProperty("accessCode")[0].GetProperty("parameters").GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task ARenamedProperty_IsKeyedByTheNameTheClientSent()
    {
        var errors = await PostAsync(new { reason = "r" });

        Assert.True(errors.TryGetProperty("tag", out _));
        Assert.False(errors.TryGetProperty("renamedOnTheWire", out _));
    }

    [Fact]
    public async Task ANestedModel_IsKeyedByItsPath()
    {
        var errors = await PostAsync(new { reason = "r", owner = new { } });

        Assert.Equal(ValidationCodes.Required, Code(errors, "owner.postcode"));
    }

    [Fact]
    public async Task ACollectionElement_IsKeyedByItsIndex()
    {
        var errors = await PostAsync(new { reason = "r", members = new[] { new { }, new { } } });

        Assert.Equal(ValidationCodes.Required, Code(errors, "members[0].postcode"));
        Assert.Equal(ValidationCodes.Required, Code(errors, "members[1].postcode"));
    }

    [Fact]
    public async Task TheDocumentIsAProblemDocument()
    {
        var (response, body) = await PostRawAsync(new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("validation_error", body.GetProperty("type").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("One or more validation errors occurred.", body.GetProperty("title").GetString());
    }

    private static string? Code(JsonElement errors, string property) =>
        errors.GetProperty(property)[0].GetProperty("type").GetString();

    private static async Task<JsonElement> PostAsync(object payload) =>
        (await PostRawAsync(payload)).Body.GetProperty("errors");

    private static async Task<(HttpResponseMessage Response, JsonElement Body)> PostRawAsync(object payload)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services
            .AddControllers(options => options.Conventions.Add(new CodedValidationConvention()))
            .AddApplicationPart(typeof(ValidationRoundTripTests).Assembly);

        var app = builder.Build();
        app.MapControllers();

        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/round-trip", payload);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(raw), $"Empty body, status {(int)response.StatusCode}.");
        var body = JsonSerializer.Deserialize<JsonElement>(raw);

        await app.StopAsync();
        return (response, body);
    }

    /// <summary>Applies the coded filter the way the Api's convention applies it to every controller.</summary>
    private sealed class CodedValidationConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller) => controller.Filters.Add(new CodedValidationFilter());
    }

    private sealed class CodedValidationFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid)
            {
                return;
            }

            var rootTypes = context.ActionDescriptor.Parameters
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            context.Result = new ObjectResult(
                ValidationProblemFactory.FromModelState(context.ModelState, rootTypes))
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}

public sealed class RoundTripInner
{
    [Required]
    public string? Postcode { get; set; }
}

public sealed class RoundTripModel
{
    [Required]
    public string? Reason { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [Required]
    [StringLength(25)]
    public string? AccessCode { get; set; }

    [StringLength(200, MinimumLength = 5)]
    public string? Bounded { get; set; }

    [Range(1, 100)]
    public int Seats { get; set; } = 1;

    [EmailAddress]
    public string? Email { get; set; }

    [JsonPropertyName("tag")]
    [Required]
    public string? RenamedOnTheWire { get; set; }

    public RoundTripInner? Owner { get; set; }

    public List<RoundTripInner>? Members { get; set; }
}

[Route("round-trip")]
public sealed class RoundTripController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromBody] RoundTripModel model) => Ok();
}
