using Bit.Services.Pam.Api.Endpoints.Filters;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Core.Models.Api;
using Bit.Pam.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints.Filters;

public class PamValidationEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_InvalidRequestModel_ReturnsErrorResponseModel400AndSkipsNext()
    {
        // Verdict is [Required] and left null -> invalid.
        var context = CreateContext(new AccessDecisionRequestModel());
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, next);

        Assert.False(nextCalled);
        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Equal("The model state is invalid.", jsonResult.Value!.Message);
        Assert.True(jsonResult.Value.ValidationErrors!.ContainsKey(nameof(AccessDecisionRequestModel.Verdict)));
    }

    [Fact]
    public async Task InvokeAsync_ValidRequestModel_CallsNext()
    {
        var context = CreateContext(new AccessDecisionRequestModel { Verdict = AccessDecisionVerdict.Approve });
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task InvokeAsync_NonRequestModelArguments_AreIgnored()
    {
        // Route/service-style arguments (a Guid, a string) are not request models and must not be validated.
        var context = CreateContext(Guid.NewGuid(), "not-a-model");
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    // Use DefaultEndpointFilterInvocationContext's params constructor rather than the static Create(...), whose
    // generic overload would treat a passed object[] as one argument instead of spreading it.
    private static EndpointFilterInvocationContext CreateContext(params object[] arguments) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), arguments);
}
