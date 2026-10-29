using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Utilities.v2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships;

public class PartnershipErrorResultsTests
{
    private const string RequestId = "test-request-id";

    public static TheoryData<Error, int, string> PartnershipErrors => new()
    {
        { new InvalidExternalId(), StatusCodes.Status400BadRequest, "invalid_external_id" },
        { new UserIdRequired(), StatusCodes.Status400BadRequest, "user_id_required" },
        { new EffectiveAtInFuture(), StatusCodes.Status422UnprocessableEntity, "effective_at_in_future" },
        { new PartnershipNotFound(), StatusCodes.Status404NotFound, "not_found" },
        { new EntitlementNotProvisioned(), StatusCodes.Status404NotFound, "not_provisioned" },
        { new PartnershipAlreadyExists(), StatusCodes.Status409Conflict, "partnership_already_exists" },
        { new PartnershipNotActive(), StatusCodes.Status409Conflict, "partnership_not_active" },
        { new EntitlementAlreadyBound(), StatusCodes.Status409Conflict, "already_bound" },
    };

    [Theory]
    [MemberData(nameof(PartnershipErrors))]
    public void FromError_PartnershipError_MapsToStatusAndCode(Error error, int expectedStatus, string expectedCode)
    {
        var result = PartnershipErrorResults.FromError(error, CreateHttpContext());

        Assert.Equal(expectedStatus, result.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal(expectedCode, body.Code);
        Assert.Equal(error.Message, body.Message);
    }

    public static TheoryData<Error, int, string> NonPartnershipErrors => new()
    {
        { new OtherBadRequest(), StatusCodes.Status400BadRequest, "invalid_request" },
        { new OtherNotFound(), StatusCodes.Status404NotFound, "not_found" },
        { new OtherConflict(), StatusCodes.Status409Conflict, "conflict" },
    };

    [Theory]
    [MemberData(nameof(NonPartnershipErrors))]
    public void FromError_ErrorWithoutPartnershipCode_UsesFamilyCode(Error error, int expectedStatus, string expectedCode)
    {
        var result = PartnershipErrorResults.FromError(error, CreateHttpContext());

        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, Assert.IsType<PartnershipErrorResponseModel>(result.Value).Code);
    }

    [Theory]
    [MemberData(nameof(UnmappedErrors))]
    public void FromError_InternalOrUnknownError_Returns500WithGenericMessage(Error error)
    {
        var result = PartnershipErrorResults.FromError(error, CreateHttpContext());

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal("internal_error", body.Code);
        Assert.Equal(PartnershipErrorResults.InternalErrorMessage, body.Message);
    }

    public static TheoryData<Error> UnmappedErrors => new()
    {
        new OtherInternal(),
        new OtherUnknown(),
    };

    [Fact]
    public void RateLimited_Returns429WithRateLimitedCode()
    {
        var result = PartnershipErrorResults.RateLimited(CreateHttpContext());

        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        Assert.Equal("rate_limited", Assert.IsType<PartnershipErrorResponseModel>(result.Value).Code);
    }

    [Fact]
    public void InvalidRequest_Returns400WithInvalidRequestCode()
    {
        var result = PartnershipErrorResults.InvalidRequest(CreateHttpContext());

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("invalid_request", Assert.IsType<PartnershipErrorResponseModel>(result.Value).Code);
    }

    [Fact]
    public void Unauthorized_Returns401WithUnauthorizedCode()
    {
        var result = PartnershipErrorResults.Unauthorized(CreateHttpContext());

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("unauthorized", Assert.IsType<PartnershipErrorResponseModel>(result.Value).Code);
    }

    [Fact]
    public void Forbidden_Returns403WithForbiddenCode()
    {
        var result = PartnershipErrorResults.Forbidden(CreateHttpContext());

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("forbidden", Assert.IsType<PartnershipErrorResponseModel>(result.Value).Code);
    }

    [Fact]
    public void FromError_Envelope_HasErrorObjectAndRequestId()
    {
        ObjectResult result = PartnershipErrorResults.FromError(new PartnershipNotFound(), CreateHttpContext());

        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal("error", body.Object);
        Assert.Equal(RequestId, body.RequestId);
    }

    private static HttpContext CreateHttpContext() => new DefaultHttpContext { TraceIdentifier = RequestId };

    private record OtherBadRequest() : BadRequestError("Bad request.");
    private record OtherNotFound() : NotFoundError;
    private record OtherConflict() : ConflictError("Conflict.");
    private record OtherInternal() : InternalError("Sensitive internal detail.");
    private record OtherUnknown() : Error("Sensitive unknown detail.");
}
