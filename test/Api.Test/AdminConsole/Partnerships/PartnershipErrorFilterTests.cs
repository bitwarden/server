using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships;

public class PartnershipErrorFilterTests
{
    private const string RequestId = "test-request-id";
    private const string SensitiveExceptionText = "Violation of UNIQUE KEY constraint. Duplicate key value is (ext-123).";

    [Fact]
    public void OnException_Returns500EnvelopeWithRequestId()
    {
        var context = CreateExceptionContext(new InvalidOperationException(SensitiveExceptionText));

        new PartnershipErrorFilter().OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal("internal_error", body.Code);
        Assert.Equal(RequestId, body.RequestId);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_DoesNotLeakExceptionText()
    {
        var exception = new InvalidOperationException(SensitiveExceptionText,
            new Exception("inner secret detail"));
        var context = CreateExceptionContext(exception);

        new PartnershipErrorFilter().OnException(context);

        var json = JsonSerializer.Serialize(Assert.IsType<ObjectResult>(context.Result).Value);
        Assert.DoesNotContain("ext-123", json);
        Assert.DoesNotContain("UNIQUE KEY", json);
        Assert.DoesNotContain("inner secret detail", json);
        Assert.DoesNotContain(nameof(InvalidOperationException), json);
    }

    [Fact]
    public void OnActionExecuting_InvalidModelState_Returns400Envelope()
    {
        var context = CreateActionExecutingContext(new Dictionary<string, object?> { ["model"] = new object() });
        context.ModelState.AddModelError("externalId", "The value 'ext-123' is not valid.");

        new PartnershipErrorFilter().OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal("invalid_request", body.Code);
        Assert.DoesNotContain("ext-123", body.Message);
    }

    [Fact]
    public void OnActionExecuting_NullModelArgument_Returns400Envelope()
    {
        var context = CreateActionExecutingContext(new Dictionary<string, object?> { ["model"] = null });

        new PartnershipErrorFilter().OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public void OnActionExecuting_ValidModel_DoesNotShortCircuit()
    {
        var context = CreateActionExecutingContext(new Dictionary<string, object?> { ["model"] = new object() });

        new PartnershipErrorFilter().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    private static ActionContext CreateActionContext()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = RequestId,
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }

    private static ExceptionContext CreateExceptionContext(Exception exception) =>
        new(CreateActionContext(), []) { Exception = exception };

    private static ActionExecutingContext CreateActionExecutingContext(IDictionary<string, object?> arguments) =>
        new(CreateActionContext(), [], arguments, controller: new object());
}
