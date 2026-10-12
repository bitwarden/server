using Bit.Core.Models.Api;
using Bit.Pam.Enums;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.Api.Endpoints.Filters;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Test.Api.Models.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints.Filters;

public class PamValidationEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_InvalidRequestModel_ReturnsErrorResponseModel400AndSkipsNext()
    {
        // Verdict is [Required] and left null.
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

    // LastKnownRevisionDate is nullable so an omitted field fails [Required], rather than binding to DateTime.MinValue
    // and reaching the revision-drift guard.
    [Fact]
    public async Task InvokeAsync_CipherUpdateWithoutLastKnownRevisionDate_Returns400()
    {
        var context = CreateContext(new SubmitCipherUpdateRequestModel { Data = "{\"rotated\":true}" });
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
        Assert.True(jsonResult.Value!.ValidationErrors!.ContainsKey(
            nameof(SubmitCipherUpdateRequestModel.LastKnownRevisionDate)));
    }

    // The report enums are nullable for the same reason: an omitted non-nullable enum binds to its zero member, which
    // is the reassuring answer for both SyncState and SessionTermination.
    [Fact]
    public async Task InvokeAsync_FailureReportWithoutSyncState_Returns400()
    {
        var context = CreateContext(new ReportRotationFailedRequestModel { ErrorCode = "target_unreachable" });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(ReportRotationFailedRequestModel.SyncState), jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_FailureReportWithOutOfRangeSyncState_Returns400()
    {
        // Without [EnumDataType] an undefined member deserializes and validates, reaching the attempt record as a
        // sync state nothing can interpret.
        var context = CreateContext(new ReportRotationFailedRequestModel
        {
            ErrorCode = "target_unreachable",
            SyncState = (PamRotationSyncState)99,
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(ReportRotationFailedRequestModel.SyncState), jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_SuccessReportWithoutSessionTermination_Returns400()
    {
        var context = CreateContext(new ReportRotationSucceededRequestModel());

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(
            nameof(ReportRotationSucceededRequestModel.SessionTermination),
            jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_TargetSystemRegistrationWithoutMethod_ReportsOnlyTheOmittedMethod()
    {
        var context = CreateContext(new RegisterTargetSystemRequestModel { Name = "db-prod" });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Equal(
            [nameof(RegisterTargetSystemRequestModel.Method)],
            jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_TargetSystemRegistrationWithOutOfRangeMethod_Returns400()
    {
        // Without [EnumDataType] an undefined member deserializes and validates, reaching RegisterAsync as a method
        // nothing can dispatch on.
        var context = CreateContext(new RegisterTargetSystemRequestModel
        {
            Name = "db-prod",
            Method = (PamTargetSystemMethod)99,
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(RegisterTargetSystemRequestModel.Method), jsonResult.Value!.ValidationErrors!.Keys);
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

    [Fact]
    public async Task InvokeAsync_NestedRequestModelViolatesARangeAttribute_Returns400()
    {
        // NestedRangeRequestModel is only reached nested, and TryValidateObject does not recurse.
        var context = CreateContext(new ParentWithNestedRequestModel
        {
            Name = "Corp SQL",
            Nested = new NestedRangeRequestModel { MinLength = 0, MaxLength = 0 },
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(NestedRangeRequestModel.MinLength), jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_NestedRequestModelViolatesItsValidatableObjectRule_Returns400()
    {
        var context = CreateContext(new ParentWithNestedRequestModel
        {
            Name = "Corp SQL",
            Nested = new NestedRangeRequestModel { MinLength = 32, MaxLength = 16 },
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(
            "MinLength must not be greater than MaxLength.",
            jsonResult.Value!.ValidationErrors![nameof(NestedRangeRequestModel.MinLength)]);
    }

    [Fact]
    public async Task InvokeAsync_ValidNestedRequestModel_CallsNext()
    {
        var nextCalled = false;
        var context = CreateContext(new ParentWithNestedRequestModel
        {
            Name = "Corp SQL",
            Nested = new NestedRangeRequestModel { MinLength = 16, MaxLength = 32 },
        });
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    // No shipped request model holds a collection of request models, so this is the only coverage of that branch.
    [Fact]
    public async Task InvokeAsync_NestedRequestModelInACollectionViolatesAnAttribute_Returns400()
    {
        var context = CreateContext(new ParentWithChildrenRequestModel
        {
            Children = [new ChildRequestModel { Value = 1 }, new ChildRequestModel { Value = 99 }],
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(ChildRequestModel.Value), jsonResult.Value!.ValidationErrors!.Keys);
    }

    // Both models are valid, so reaching next proves the walk terminated.
    [Fact]
    public async Task InvokeAsync_CyclicNestedRequestModel_TerminatesAndCallsNext()
    {
        var nextCalled = false;
        var parent = new CyclicRequestModel { Value = 1 };
        var child = new CyclicRequestModel { Value = 1, Other = parent };
        parent.Other = child;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        };

        var result = await new PamValidationEndpointFilter().InvokeAsync(CreateContext(parent), next);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    private static EndpointFilterDelegate NotCalled() =>
        _ => throw new Xunit.Sdk.XunitException("The filter should have short-circuited before calling next.");

    // Uses the params constructor, since the generic Create overload would take a passed object[] as one argument.
    private static EndpointFilterInvocationContext CreateContext(params object[] arguments) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), arguments);
}
