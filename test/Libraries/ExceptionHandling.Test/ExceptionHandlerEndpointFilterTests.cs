using System.Net;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bit.ExceptionHandling.Test;

/// <summary>
/// Tests for <see cref="ExceptionHandlingEndpointExtensions.WithBasicExceptionHandling{TBuilder}"/>,
/// covering both the exception-to-response mapping produced by the endpoint filter and the
/// <see cref="ProducesResponseTypeMetadata"/> that the extension registers on the endpoint.
/// </summary>
public class ExceptionHandlerEndpointFilterTests
{
    /// <summary>
    /// Thin wrapper around a started <see cref="WebApplication"/> backed by <see cref="TestServer"/>.
    /// The single endpoint GET /test has <c>WithBasicExceptionHandling</c> applied.
    /// </summary>
    private sealed class TestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Client { get; }

        private TestApp(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        /// <summary>
        /// Creates and starts a test app whose single endpoint delegates to <paramref name="handler"/>.
        /// Set <paramref name="isDevelopment"/> to exercise the development-only exception detail path.
        /// </summary>
        public static async Task<TestApp> CreateAsync(Func<IResult> handler, bool isDevelopment = false)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                EnvironmentName = isDevelopment ? Environments.Development : Environments.Production,
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            var app = builder.Build();

            app.MapGet("/test", handler).WithBasicExceptionHandling();

            await app.StartAsync();
            return new TestApp(app);
        }

        public IReadOnlyList<Endpoint> Endpoints =>
            ((IEndpointRouteBuilder)_app).DataSources.SelectMany(ds => ds.Endpoints).ToList();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public async Task BadRequestException_Returns400WithExceptionMessage()
    {
        await using var app = await TestApp.CreateAsync(() => throw new BadRequestException("bad input"));

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad input", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task BadRequestExceptionWithModelState_Returns400WithValidationErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("email", "Email is required.");
        await using var app = await TestApp.CreateAsync(() => throw new BadRequestException(modelState));

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorValues = body.GetProperty("validationErrors")
                              .GetProperty("email")
                              .EnumerateArray()
                              .Select(e => e.GetString());
        Assert.Contains("Email is required.", errorValues);
    }

    [Fact]
    public async Task NotSupportedExceptionWithMessage_Returns400()
    {
        await using var app = await TestApp.CreateAsync(() => throw new NotSupportedException("feature not supported"));

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("feature not supported", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task NotSupportedExceptionWithEmptyMessage_FallsToDefault500()
    {
        // The filter only handles NotSupportedException when the message is non-whitespace.
        await using var app = await TestApp.CreateAsync(() => throw new NotSupportedException(""));

        var response = await app.Client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationException_Returns402()
    {
        await using var app = await TestApp.CreateAsync(() => throw new ApplicationException());

        var response = await app.Client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task NotFoundException_Returns404WithStandardMessage()
    {
        // The filter always uses "Resource not found." regardless of the exception message.
        await using var app = await TestApp.CreateAsync(() => throw new NotFoundException("ignored"));

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnauthorizedAccessException_Returns401()
    {
        await using var app = await TestApp.CreateAsync(() => throw new UnauthorizedAccessException());

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Unauthorized.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ConflictException_Returns409WithExceptionMessage()
    {
        await using var app = await TestApp.CreateAsync(() => throw new ConflictException("already exists"));

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("already exists", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AggregateException_FallsToDefault500()
    {
        await using var app = await TestApp.CreateAsync(
            () => throw new AggregateException(
                new InvalidOperationException("first error"),
                new InvalidOperationException("second error")));

        var response = await app.Client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithGenericMessage()
    {
        await using var app = await TestApp.CreateAsync(() => throw new InvalidOperationException("internal detail"));

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("An unhandled server error has occurred.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task NoException_PassesThroughResult()
    {
        await using var app = await TestApp.CreateAsync(() => Results.Ok());

        var response = await app.Client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentEnvironment_ExposesExceptionDetails()
    {
        await using var app = await TestApp.CreateAsync(
            () => throw new InvalidOperationException("inner details"),
            isDevelopment: true);

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.Equal("inner details", body.GetProperty("exceptionMessage").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("exceptionStackTrace").GetString()));
    }

    [Fact]
    public async Task ProductionEnvironment_DoesNotExposeExceptionDetails()
    {
        await using var app = await TestApp.CreateAsync(
            () => throw new InvalidOperationException("inner details"),
            isDevelopment: false);

        var response = await app.Client.GetAsync("/test");
        var body = await ReadJsonAsync(response);

        Assert.True(
            !body.TryGetProperty("exceptionMessage", out var val) || val.ValueKind == JsonValueKind.Null,
            "exceptionMessage must not be present in production responses");
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status402PaymentRequired)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status409Conflict)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task WithBasicExceptionHandling_RegistersProducesResponseTypeMetadataForStatusCode(int statusCode)
    {
        await using var app = await TestApp.CreateAsync(() => Results.Ok());

        var metadata = app.Endpoints
            .Single()
            .Metadata
            .GetOrderedMetadata<ProducesResponseTypeMetadata>();

        Assert.Contains(metadata, m => m.StatusCode == statusCode && m.Type == typeof(ErrorResponseModel));
    }
}
