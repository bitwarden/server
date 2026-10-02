using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

using static Bit.Api.Test.Billing.Utilities;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationBillingController))]
[SutProviderCustomize]
public class OrganizationBillingControllerTests
{
    [Theory, BitAutoData]
    public async Task GetHistoryAsync_Unauthorized_ReturnsUnauthorized(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ViewBillingHistory(organizationId).Returns(false);

        var result = await sutProvider.Sut.GetHistoryAsync(organizationId);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_OrganizationNotFound_ReturnsNotFound(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ViewBillingHistory(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns((Organization)null);

        var result = await sutProvider.Sut.GetHistoryAsync(organizationId);

        AssertNotFound(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetHistoryAsync_OK(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().ViewBillingHistory(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        // Manually create a BillingHistoryInfo object to avoid requiring AutoFixture to create HttpResponseHeaders
        var billingInfo = new BillingHistoryInfo();

        sutProvider.GetDependency<IStripePaymentService>().GetBillingHistoryAsync(organization).Returns(billingInfo);

        // Act
        var result = await sutProvider.Sut.GetHistoryAsync(organizationId);

        // Assert
        var okResult = Assert.IsType<Ok<BillingHistoryInfo>>(result);
        Assert.Equal(billingInfo, okResult.Value);
    }

    [Theory, BitAutoData]
    public async Task ChangePlanSubscriptionFrequencyAsync_Unauthorized_ReturnsUnauthorized(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        // Arrange
        var request = new ChangePlanFrequencyRequest { NewPlanType = PlanType.EnterpriseMonthly };
        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organizationId).Returns(false);

        // Act
        var result = await sutProvider.Sut.ChangePlanSubscriptionFrequencyAsync(organizationId, request);

        // Assert
        AssertUnauthorized(result);

        await sutProvider.GetDependency<IOrganizationBillingService>()
            .DidNotReceive()
            .UpdateSubscriptionPlanFrequency(Arg.Any<Organization>(), Arg.Any<PlanType>());
    }

    [Theory, BitAutoData]
    public async Task ChangePlanSubscriptionFrequencyAsync_OrganizationNotFound_ReturnsNotFound(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        // Arrange
        var request = new ChangePlanFrequencyRequest { NewPlanType = PlanType.EnterpriseMonthly };
        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns((Organization)null);

        // Act
        var result = await sutProvider.Sut.ChangePlanSubscriptionFrequencyAsync(organizationId, request);

        // Assert
        AssertNotFound(result);

        await sutProvider.GetDependency<IOrganizationBillingService>()
            .DidNotReceive()
            .UpdateSubscriptionPlanFrequency(Arg.Any<Organization>(), Arg.Any<PlanType>());
    }

    [Theory, BitAutoData]
    public async Task ChangePlanSubscriptionFrequencyAsync_SamePlan_ReturnsBadRequest(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        var request = new ChangePlanFrequencyRequest { NewPlanType = PlanType.EnterpriseAnnually };

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        // Act
        var result = await sutProvider.Sut.ChangePlanSubscriptionFrequencyAsync(organizationId, request);

        // Assert
        var badRequest = Assert.IsType<BadRequest<ErrorResponseModel>>(result);
        Assert.Equal("Organization is already on the requested plan frequency.", badRequest.Value!.Message);

        await sutProvider.GetDependency<IOrganizationBillingService>()
            .DidNotReceive()
            .UpdateSubscriptionPlanFrequency(Arg.Any<Organization>(), Arg.Any<PlanType>());
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually, PlanType.Free)]
    [BitAutoData(PlanType.EnterpriseAnnually, PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.EnterpriseAnnually, PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually, PlanType.Free)]
    [BitAutoData(PlanType.TeamsAnnually, PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually, PlanType.EnterpriseMonthly)]
    public async Task ChangePlanSubscriptionFrequencyAsync_DifferentTier_ReturnsBadRequest(
        PlanType currentPlan,
        PlanType requestedPlan,
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        // Arrange
        organization.PlanType = currentPlan;
        var request = new ChangePlanFrequencyRequest { NewPlanType = requestedPlan };

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        // Act
        var result = await sutProvider.Sut.ChangePlanSubscriptionFrequencyAsync(organizationId, request);

        // Assert
        var badRequest = Assert.IsType<BadRequest<ErrorResponseModel>>(result);
        Assert.Equal("Plan frequency changes must stay within the same product tier.", badRequest.Value!.Message);

        await sutProvider.GetDependency<IOrganizationBillingService>()
            .DidNotReceive()
            .UpdateSubscriptionPlanFrequency(Arg.Any<Organization>(), Arg.Any<PlanType>());
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually, PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseMonthly, PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsAnnually, PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsMonthly, PlanType.TeamsAnnually)]
    public async Task ChangePlanSubscriptionFrequencyAsync_SameTierDifferentFrequency_ReturnsOk(
        PlanType currentPlan,
        PlanType requestedPlan,
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        // Arrange
        organization.PlanType = currentPlan;
        var request = new ChangePlanFrequencyRequest { NewPlanType = requestedPlan };

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        // Act
        var result = await sutProvider.Sut.ChangePlanSubscriptionFrequencyAsync(organizationId, request);

        // Assert
        Assert.IsType<Ok>(result);

        await sutProvider.GetDependency<IOrganizationBillingService>()
            .Received(1)
            .UpdateSubscriptionPlanFrequency(organization, requestedPlan);
    }
}
