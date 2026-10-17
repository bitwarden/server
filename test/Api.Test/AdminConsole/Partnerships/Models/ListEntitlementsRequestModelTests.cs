using Bit.Api.AdminConsole.Partnerships.Models.Request;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships.Models;

public class ListEntitlementsRequestModelTests
{
    [Theory]
    [InlineData("provisioned", PartnershipEntitlementState.Provisioned)]
    [InlineData("active", PartnershipEntitlementState.Active)]
    [InlineData("suspended", PartnershipEntitlementState.Suspended)]
    [InlineData("canceled", PartnershipEntitlementState.Canceled)]
    public void ToRequest_KnownState_MapsToEnum(string state, PartnershipEntitlementState expected)
    {
        var request = new ListEntitlementsRequestModel { State = state }.ToRequest(Guid.NewGuid()).AsSuccess;

        Assert.Equal(expected, request.State);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("1")]
    [InlineData("pending")]
    [InlineData("")]
    public void ToRequest_UnknownState_ReturnsInvalidRequest(string state)
    {
        var error = new ListEntitlementsRequestModel { State = state }.ToRequest(Guid.NewGuid()).AsError;

        Assert.Equal("invalid_request", Assert.IsType<InvalidEntitlementStateFilter>(error).Code);
    }

    [Fact]
    public void ToRequest_RevisedAfterWithOffset_ConvertsToUtc()
    {
        var request = new ListEntitlementsRequestModel { RevisedAfter = "2026-10-16T08:00:00.1234567-04:00" }
            .ToRequest(Guid.NewGuid()).AsSuccess;

        var expected = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        Assert.Equal(expected, request.RevisedAfter);
        Assert.Equal(DateTimeKind.Utc, request.RevisedAfter!.Value.Kind);
    }

    [Fact]
    public void ToRequest_RevisedAfterWithoutOffset_IsUtc()
    {
        var request = new ListEntitlementsRequestModel { RevisedAfter = "2026-10-16T12:00:00" }
            .ToRequest(Guid.NewGuid()).AsSuccess;

        Assert.Equal(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), request.RevisedAfter);
    }

    [Fact]
    public void ToRequest_InvalidRevisedAfter_ReturnsInvalidRequest()
    {
        var error = new ListEntitlementsRequestModel { RevisedAfter = "yesterday" }.ToRequest(Guid.NewGuid()).AsError;

        Assert.Equal("invalid_request", Assert.IsType<InvalidRevisedAfter>(error).Code);
    }

    [Fact]
    public void ToRequest_CarriesPartnershipAndToken()
    {
        var partnershipId = Guid.NewGuid();

        var request = new ListEntitlementsRequestModel { ContinuationToken = "token" }.ToRequest(partnershipId)
            .AsSuccess;

        Assert.Equal(partnershipId, request.OrganizationPartnershipId);
        Assert.Equal("token", request.ContinuationToken);
        Assert.Null(request.State);
        Assert.Null(request.RevisedAfter);
    }
}
