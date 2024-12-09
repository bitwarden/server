using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    public async Task GetMetadataAsync_Unauthorized_ReturnsUnauthorized(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessMembersTab(organizationId).Returns(false);

        var result = await sutProvider.Sut.GetMetadataAsync(organizationId);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task GetMetadataAsync_MetadataNull_NotFound(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationBillingService>().GetMetadata(organizationId).Returns((OrganizationMetadata)null);

        var result = await sutProvider.Sut.GetMetadataAsync(organizationId);

        AssertNotFound(result);
    }

    [Theory, BitAutoData]
    public async Task GetMetadataAsync_OK(
        Guid organizationId,
        SutProvider<OrganizationBillingController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationBillingService>().GetMetadata(organizationId)
            .Returns(new OrganizationMetadata(true, true, true, true, true));

        var result = await sutProvider.Sut.GetMetadataAsync(organizationId);

        Assert.IsType<Ok<OrganizationMetadataResponse>>(result);

        var response = ((Ok<OrganizationMetadataResponse>)result).Value;

        Assert.True(response.IsEligibleForSelfHost);
        Assert.True(response.IsManaged);
        Assert.True(response.IsOnSecretsManagerStandalone);
        Assert.True(response.IsSubscriptionUnpaid);
        Assert.True(response.HasSubscription);
    }

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

        sutProvider.GetDependency<IPaymentService>().GetBillingHistoryAsync(organization).Returns(billingInfo);

        // Act
        var result = await sutProvider.Sut.GetHistoryAsync(organizationId);

        // Assert
        var okResult = Assert.IsType<Ok<BillingHistoryInfo>>(result);
        Assert.Equal(billingInfo, okResult.Value);
    }
}
