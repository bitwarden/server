using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using ProviderOrganization = Bit.Core.AdminConsole.Entities.Provider.ProviderOrganization;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(ProviderOrganizationController))]
[SutProviderCustomize]
public class ProviderOrganizationControllerTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_FFDisabled_NotFound(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateProviderOrganizationRequestBody requestBody,
        SutProvider<ProviderOrganizationController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(false);

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NotProviderAdmin_Unauthorized(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateProviderOrganizationRequestBody requestBody,
        SutProvider<ProviderOrganizationController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(false);

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NoProvider_NotFound(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateProviderOrganizationRequestBody requestBody,
        SutProvider<ProviderOrganizationController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .ReturnsNull();

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NoProviderOrganization_NotFound(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateProviderOrganizationRequestBody requestBody,
        Provider provider,
        SutProvider<ProviderOrganizationController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .ReturnsNull();

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NoOrganization_ServerError(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateProviderOrganizationRequestBody requestBody,
        Provider provider,
        ProviderOrganization providerOrganization,
        SutProvider<ProviderOrganizationController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(providerOrganization.OrganizationId)
            .ReturnsNull();

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<ProblemHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NoContent(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateProviderOrganizationRequestBody requestBody,
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<ProviderOrganizationController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(providerOrganization.OrganizationId)
            .Returns(organization);

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        await sutProvider.GetDependency<IAssignSeatsToClientOrganizationCommand>().Received(1)
            .AssignSeatsToClientOrganization(
                provider,
                organization,
                requestBody.AssignedSeats);

        Assert.IsType<Ok>(result);
    }
}
