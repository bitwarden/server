using System.Security.Claims;
using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Admin.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationTrialController))]
[SutProviderCustomize]
public class OrganizationTrialControllerTests
{
    private const string _actorEmail = "sales.rep@bitwarden.com";
    private const string _genericError =
        "The trial could not be extended. Please try again or contact support if the problem persists.";

    private static void Arrange(SutProvider<OrganizationTrialController> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35092AuthSalesAssistedTrials)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, _actorEmail)], "Test"))
        };
        sutProvider.Sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
        sutProvider.Sut.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
    }

    private static void AssertRedirectsToOrganizationEdit(IActionResult result, Guid organizationId)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal("Organizations", redirect.ControllerName);
        Assert.Equal(organizationId, redirect.RouteValues!["id"]);
    }

    [Theory, BitAutoData]
    public async Task Extend_FeatureFlagOff_ReturnsNotFound(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        var result = await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 5 });

        Assert.IsType<NotFoundResult>(result);
        await sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .DidNotReceiveWithAnyArgs()
            .Run(default!, default);
    }

    [Theory, BitAutoData]
    public async Task Extend_OrganizationNotFound_ReturnsNotFound(
        Guid organizationId,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35092AuthSalesAssistedTrials)
            .Returns(true);

        var result = await sutProvider.Sut.ExtendAsync(organizationId, new ExtendTrialModel { Days = 5 });

        Assert.IsType<NotFoundResult>(result);
        await sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .DidNotReceiveWithAnyArgs()
            .Run(default!, default);
    }

    [Theory, BitAutoData]
    public async Task Extend_InvalidModelState_SetsErrorAndRedirectsWithoutRunningCommand(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.Sut.ModelState.AddModelError(nameof(ExtendTrialModel.Days), "Days must be between 1 and 30.");

        var result = await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 31 });

        AssertRedirectsToOrganizationEdit(result, organization.Id);
        Assert.Equal("Days must be between 1 and 30.", sutProvider.Sut.TempData["Error"]);
        await sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .DidNotReceiveWithAnyArgs()
            .Run(default!, default);
    }

    [Theory, BitAutoData]
    public async Task Extend_CommandSucceeds_SetsSuccessAndRedirects(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        var newTrialEnd = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .Run(organization, 7)
            .Returns(new BillingCommandResult<DateTime>(newTrialEnd));

        var result = await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 7 });

        AssertRedirectsToOrganizationEdit(result, organization.Id);
        Assert.Equal("Trial extended to 2026-11-15 UTC.", sutProvider.Sut.TempData["Success"]);
        Assert.False(sutProvider.Sut.TempData.ContainsKey("Error"));
    }

    [Theory, BitAutoData]
    public async Task Extend_CommandSucceeds_WritesAuditLogWithActorDaysAndDates(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        var newTrialEnd = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .Run(organization, 7)
            .Returns(new BillingCommandResult<DateTime>(newTrialEnd));

        await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 7 });

        sutProvider.GetDependency<ILogger<OrganizationTrialController>>()
            .Received(1)
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(state =>
                    state.ToString()!.Contains(_actorEmail) &&
                    state.ToString()!.Contains(organization.Id.ToString()) &&
                    state.ToString()!.Contains("by 7 days") &&
                    state.ToString()!.Contains("2026-11-08 12:00:00Z") &&
                    state.ToString()!.Contains("2026-11-15 12:00:00Z")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Extend_CommandReturnsBadRequest_DoesNotWriteAuditLog(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .Run(organization, 7)
            .Returns(new BillingCommandResult<DateTime>(new BadRequest("nope")));

        await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 7 });

        sutProvider.GetDependency<ILogger<OrganizationTrialController>>()
            .DidNotReceive()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Extend_CommandReturnsBadRequest_SetsErrorWithResponseMessage(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        const string message = "Trial cannot be extended because the linked subscription is not in a trialing status.";
        sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .Run(organization, 7)
            .Returns(new BillingCommandResult<DateTime>(new BadRequest(message)));

        var result = await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 7 });

        AssertRedirectsToOrganizationEdit(result, organization.Id);
        Assert.Equal(message, sutProvider.Sut.TempData["Error"]);
    }

    [Theory, BitAutoData]
    public async Task Extend_CommandReturnsUnhandled_SetsGenericError(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .Run(organization, 7)
            .Returns(new BillingCommandResult<DateTime>(new Unhandled(new Exception("boom"))));

        var result = await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 7 });

        AssertRedirectsToOrganizationEdit(result, organization.Id);
        Assert.Equal(_genericError, sutProvider.Sut.TempData["Error"]);
    }

    [Theory, BitAutoData]
    public async Task Extend_CommandReturnsConflict_SetsGenericError(
        Organization organization,
        SutProvider<OrganizationTrialController> sutProvider)
    {
        Arrange(sutProvider, organization);
        sutProvider.GetDependency<IExtendOrganizationTrialCommand>()
            .Run(organization, 7)
            .Returns(new BillingCommandResult<DateTime>(new Conflict("conflict")));

        var result = await sutProvider.Sut.ExtendAsync(organization.Id, new ExtendTrialModel { Days = 7 });

        AssertRedirectsToOrganizationEdit(result, organization.Id);
        Assert.Equal(_genericError, sutProvider.Sut.TempData["Error"]);
    }
}
