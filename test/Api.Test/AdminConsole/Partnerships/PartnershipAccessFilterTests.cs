using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships;

public class PartnershipAccessFilterTests
{
    private const string RequestId = "test-request-id";

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly ICurrentContext _currentContext = Substitute.For<ICurrentContext>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IOrganizationPartnershipRepository _repository = Substitute.For<IOrganizationPartnershipRepository>();
    private readonly PartnershipRequestContext _partnershipRequestContext = new();
    private readonly PartnershipAccessFilter _sut;

    public PartnershipAccessFilterTests()
    {
        _currentContext.OrganizationId.Returns(_organizationId);
        _featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships).Returns(true);
        _sut = new PartnershipAccessFilter(_currentContext, _featureService, _repository, _partnershipRequestContext);
    }

    [Fact]
    public async Task NoPartnership_Returns404NotFound()
    {
        var (context, nextCalled) = await ExecuteAsync();

        AssertError(context, StatusCodes.Status404NotFound, "not_found");
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task FlagOff_Returns404WithoutLookingUpThePartnership()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships).Returns(false);
        ReturnPartnership(PartnershipStatus.Active);

        var (context, nextCalled) = await ExecuteAsync();

        AssertError(context, StatusCodes.Status404NotFound, "not_found");
        Assert.False(nextCalled());
        await _repository.DidNotReceiveWithAnyArgs().GetByOrganizationIdAsync(default);
    }

    [Fact]
    public async Task FlagOffAndNoPartnership_ReturnIdenticalResponses()
    {
        var (noPartnership, _) = await ExecuteAsync();
        _featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships).Returns(false);
        ReturnPartnership(PartnershipStatus.Active);
        var (flagOff, _) = await ExecuteAsync();

        var noPartnershipResult = Assert.IsType<ObjectResult>(noPartnership.Result);
        var flagOffResult = Assert.IsType<ObjectResult>(flagOff.Result);
        Assert.Equal(noPartnershipResult.StatusCode, flagOffResult.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(noPartnershipResult.Value), JsonSerializer.Serialize(flagOffResult.Value));
    }

    [Fact]
    public async Task NoOrganizationOnToken_Returns404()
    {
        _currentContext.OrganizationId.Returns((Guid?)null);

        var (context, nextCalled) = await ExecuteAsync();

        AssertError(context, StatusCodes.Status404NotFound, "not_found");
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task InactivePartnership_Returns403NotActive()
    {
        ReturnPartnership(PartnershipStatus.Inactive);

        var (context, nextCalled) = await ExecuteAsync();

        AssertError(context, StatusCodes.Status403Forbidden, "partnership_not_active");
        Assert.False(nextCalled());
        Assert.Throws<InvalidOperationException>(() => _partnershipRequestContext.Partnership);
    }

    [Fact]
    public async Task ActivePartnership_PopulatesContextAndContinues()
    {
        var partnership = ReturnPartnership(PartnershipStatus.Active);

        var (context, nextCalled) = await ExecuteAsync();

        Assert.Null(context.Result);
        Assert.True(nextCalled());
        Assert.Same(partnership, _partnershipRequestContext.Partnership);
    }

    private OrganizationPartnership ReturnPartnership(PartnershipStatus status)
    {
        var partnership = new OrganizationPartnership
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Name = "Partner",
            Status = status,
        };
        _repository.GetByOrganizationIdAsync(_organizationId).Returns(partnership);
        return partnership;
    }

    private async Task<(ActionExecutingContext Context, Func<bool> NextCalled)> ExecuteAsync()
    {
        var actionContext = new ActionContext(new DefaultHttpContext { TraceIdentifier = RequestId }, new RouteData(),
            new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(),
            controller: new object());
        var called = false;

        await _sut.OnActionExecutionAsync(context, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()));
        });

        return (context, () => called);
    }

    private static void AssertError(ActionExecutingContext context, int expectedStatus, string expectedCode)
    {
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal(expectedCode, body.Code);
        Assert.Equal(RequestId, body.RequestId);
    }
}
