using System.Text;
using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships.Idempotency;

public class PartnershipIdempotencyFilterTests : IDisposable
{
    private const string Path = "/partnerships/v1/things";
    private const string Body = """{"name":"a"}""";

    private readonly TestIdempotencyStore _test = new();
    private readonly PartnershipRequestContext _partnershipRequestContext = new();
    private readonly PartnershipIdempotencyFilter _sut;
    private int _executions;

    public PartnershipIdempotencyFilterTests()
    {
        _partnershipRequestContext.SetPartnership(new OrganizationPartnership { Id = Guid.NewGuid(), Name = "Partner" });
        _sut = new PartnershipIdempotencyFilter(_test.Store, _partnershipRequestContext);
    }

    [Fact]
    public async Task NoHeader_RunsActionEveryTime()
    {
        await SendAsync(key: null);
        var second = await SendAsync(key: null);

        Assert.Equal(2, _executions);
        Assert.Equal(201, second.StatusCode);
        Assert.False(second.Headers.ContainsKey(PartnershipIdempotencyFilter.ReplayedHeaderName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("tab\tkey")]
    [InlineData("clé")]
    public async Task InvalidKey_Returns400WithoutRunningAction(string key)
    {
        var response = await SendAsync(key);

        AssertError(response, StatusCodes.Status400BadRequest, PartnershipErrorResults.InvalidIdempotencyKeyCode);
        Assert.Equal(0, _executions);
    }

    [Fact]
    public async Task KeyLongerThanMax_Returns400()
    {
        var response = await SendAsync(new string('k', PartnershipIdempotencyFilter.MaxKeyLength + 1));

        AssertError(response, StatusCodes.Status400BadRequest, PartnershipErrorResults.InvalidIdempotencyKeyCode);
    }

    [Fact]
    public async Task KeyAtMaxLength_IsAccepted()
    {
        var response = await SendAsync(new string('k', PartnershipIdempotencyFilter.MaxKeyLength));

        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public async Task FirstRequest_RunsActionAndReturnsItsResponse()
    {
        var response = await SendAsync("key");

        Assert.Equal(1, _executions);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("application/json", response.ContentType);
        Assert.Equal("""{"execution":1}""", response.Body);
        Assert.False(response.Headers.ContainsKey(PartnershipIdempotencyFilter.ReplayedHeaderName));
    }

    [Fact]
    public async Task Replay_ReturnsStoredResponseWithoutRunningAction()
    {
        await SendAsync("key");

        var replay = await SendAsync("key");

        Assert.Equal(1, _executions);
        Assert.Equal(201, replay.StatusCode);
        Assert.Equal("application/json", replay.ContentType);
        Assert.Equal("""{"execution":1}""", replay.Body);
        Assert.Equal("true", replay.Headers[PartnershipIdempotencyFilter.ReplayedHeaderName]);
    }

    [Fact]
    public async Task SameKeyDifferentBody_Returns409Reused()
    {
        await SendAsync("key");

        var response = await SendAsync("key", body: """{"name":"b"}""");

        AssertError(response, StatusCodes.Status409Conflict, PartnershipErrorResults.IdempotencyKeyReusedCode);
        Assert.Equal(1, _executions);
    }

    [Fact]
    public async Task SameKeyDifferentPath_Returns409Reused()
    {
        await SendAsync("key");

        var response = await SendAsync("key", path: "/partnerships/v1/other");

        AssertError(response, StatusCodes.Status409Conflict, PartnershipErrorResults.IdempotencyKeyReusedCode);
    }

    [Fact]
    public async Task SameKeyWhileFirstRequestRuns_Returns409InProgress()
    {
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var first = SendAsync("key", action: async () =>
        {
            started.SetResult();
            await release.Task;
            return new TestResult(201, "{}");
        });
        await started.Task;

        var second = await SendAsync("key");
        release.SetResult();
        await first;

        AssertError(second, StatusCodes.Status409Conflict, PartnershipErrorResults.IdempotencyKeyInProgressCode);
        Assert.Equal(0, _executions);
    }

    [Fact]
    public async Task SameKeyOnAnotherPartnership_RunsAction()
    {
        await SendAsync("key");
        _partnershipRequestContext.SetPartnership(new OrganizationPartnership { Id = Guid.NewGuid(), Name = "Other" });

        var response = await SendAsync("key");

        Assert.Equal(2, _executions);
        Assert.False(response.Headers.ContainsKey(PartnershipIdempotencyFilter.ReplayedHeaderName));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task ClientError_IsReplayed(int statusCode)
    {
        await SendAsync("key", action: () => Task.FromResult<IActionResult>(new TestResult(statusCode, "{}")));

        var replay = await SendAsync("key");

        Assert.Equal(statusCode, replay.StatusCode);
        Assert.Equal("true", replay.Headers[PartnershipIdempotencyFilter.ReplayedHeaderName]);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(429)]
    public async Task ServerErrorOrRateLimit_IsNotStored(int statusCode)
    {
        await SendAsync("key", action: () => Task.FromResult<IActionResult>(new TestResult(statusCode, "{}")));

        var retry = await SendAsync("key");

        Assert.Equal(201, retry.StatusCode);
        Assert.Equal(1, _executions);
    }

    [Fact]
    public async Task UnhandledException_IsNotStored()
    {
        await SendAsync("key", action: () => throw new InvalidOperationException());

        var retry = await SendAsync("key");

        Assert.Equal(201, retry.StatusCode);
        Assert.Equal(1, _executions);
    }

    private async Task<Response> SendAsync(string? key, string body = Body, string path = Path,
        Func<Task<IActionResult>>? action = null)
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "request-id" };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = path;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Response.Body = new MemoryStream();
        if (key is not null)
        {
            httpContext.Request.Headers[PartnershipIdempotencyFilter.HeaderName] = key;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(),
            controller: new object());
        ActionExecutedContext? executed = null;

        await _sut.OnActionExecutionAsync(executing, async () =>
        {
            executed = new ActionExecutedContext(actionContext, [], controller: new object());
            try
            {
                executed.Result = await (action ?? DefaultAction)();
            }
            catch (Exception exception)
            {
                executed.Exception = exception;
            }

            return executed;
        });

        var result = executing.Result ?? executed?.Result;
        if (result is ObjectResult objectResult)
        {
            return new Response(objectResult.StatusCode ?? 0, null, null, httpContext.Response.Headers, objectResult);
        }

        if (result is not null)
        {
            await result.ExecuteResultAsync(actionContext);
        }

        return new Response(httpContext.Response.StatusCode, httpContext.Response.ContentType,
            Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray()), httpContext.Response.Headers,
            null);
    }

    private Task<IActionResult> DefaultAction()
    {
        var execution = Interlocked.Increment(ref _executions);
        return Task.FromResult<IActionResult>(new TestResult(201, $$"""{"execution":{{execution}}}"""));
    }

    private static void AssertError(Response response, int expectedStatus, string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(response.Error?.Value);
        Assert.Equal(expectedCode, body.Code);
    }

    private sealed record Response(int StatusCode, string? ContentType, string? Body, IHeaderDictionary Headers,
        ObjectResult? Error);

    private sealed class TestResult(int statusCode, string body) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = statusCode;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(body));
        }
    }

    public void Dispose() => _test.Dispose();
}
