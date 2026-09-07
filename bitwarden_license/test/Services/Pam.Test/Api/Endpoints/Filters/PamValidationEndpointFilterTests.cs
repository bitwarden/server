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

    // LastKnownRevisionDate is a nullable DateTime precisely so an omitted field fails [Required] here as a 400,
    // rather than binding to DateTime.MinValue and reaching the revision-drift guard as a plausible instant.
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

    // The rotation report enums are nullable for the same reason. [Required] alone would not catch an omitted
    // value on a non-nullable enum -- it only rejects null -- so the field would bind to whichever member is zero:
    // "the vault credential is still correct" for SyncState, "termination was never attempted" for
    // SessionTermination. Both are the reassuring answer, reported for an access connector that said nothing.
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
        // An omitted Method used to bind to Automatic and fail the automatic shape rules, blaming Kind and
        // PasswordPolicy. The caller never chose a method, so Method is the only honest complaint.
        var context = CreateContext(new RegisterTargetSystemRequestModel { Name = "db-prod" });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Equal(
            [nameof(RegisterTargetSystemRequestModel.Method)],
            jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_TargetSystemRegistrationWithOutOfRangeKind_Returns400()
    {
        // Kind is optional rather than [Required], but an undefined member still needs rejecting: it would be
        // stored as the integration the access connector is expected to rotate through.
        var context = CreateContext(new RegisterTargetSystemRequestModel
        {
            Name = "db-prod",
            Method = PamTargetSystemMethod.Automatic,
            Kind = (PamTargetSystemKind)99,
            PasswordPolicy = new PamPasswordPolicyRequestModel
            {
                MinLength = 16,
                MaxLength = 32,
                IncludeLowercase = true,
            },
            SupportsSessionTermination = false,
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(RegisterTargetSystemRequestModel.Kind), jsonResult.Value!.ValidationErrors!.Keys);
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

    [Fact]
    public async Task InvokeAsync_NestedRequestModelViolatesARangeAttribute_Returns400()
    {
        // PamPasswordPolicyRequestModel is only ever reached as a nested property, and TryValidateObject does not
        // recurse -- so without the filter's own walk these constraints would never run.
        var context = CreateContext(new UpdateTargetSystemRequestModel
        {
            Name = "Corp SQL",
            PasswordPolicy = new PamPasswordPolicyRequestModel
            {
                MinLength = 0,
                MaxLength = 0,
                IncludeLowercase = true,
            },
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(nameof(PamPasswordPolicyRequestModel.MinLength), jsonResult.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_NestedRequestModelViolatesItsValidatableObjectRule_Returns400()
    {
        var context = CreateContext(new UpdateTargetSystemRequestModel
        {
            Name = "Corp SQL",
            PasswordPolicy = new PamPasswordPolicyRequestModel
            {
                MinLength = 32,
                MaxLength = 16,
                IncludeLowercase = true,
            },
        });

        var result = await new PamValidationEndpointFilter().InvokeAsync(context, NotCalled());

        var jsonResult = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        Assert.Contains(
            "MinLength must not be greater than MaxLength.",
            jsonResult.Value!.ValidationErrors![nameof(PamPasswordPolicyRequestModel.MinLength)]);
    }

    [Fact]
    public async Task InvokeAsync_ValidNestedRequestModel_CallsNext()
    {
        var nextCalled = false;
        var context = CreateContext(new UpdateTargetSystemRequestModel
        {
            Name = "Corp SQL",
            PasswordPolicy = new PamPasswordPolicyRequestModel
            {
                MinLength = 16,
                MaxLength = 32,
                IncludeUppercase = true,
                IncludeLowercase = true,
                IncludeDigits = true,
            },
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

    // The filter walks IEnumerable properties element by element. No PAM request model holds a collection of
    // request models yet, so this is the only thing exercising that branch until one does.
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

    // A model reachable from itself would recurse forever without the reference-based visited set. The child is
    // valid, so reaching next at all is what proves the walk terminated.
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

    // Use DefaultEndpointFilterInvocationContext's params constructor rather than the static Create(...), whose
    // generic overload would treat a passed object[] as one argument instead of spreading it.
    private static EndpointFilterInvocationContext CreateContext(params object[] arguments) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), arguments);
}
