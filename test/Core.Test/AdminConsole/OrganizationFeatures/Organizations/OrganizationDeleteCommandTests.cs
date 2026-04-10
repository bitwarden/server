using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationDeleteCommandTests
{
    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_Success(Organization organization,
        SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        var cipherService = sutProvider.GetDependency<ICipherService>();

        await sutProvider.Sut.DeleteAsync(organization);

        await cipherService.Received(1).DeleteAttachmentsForOrganizationAsync(organization.Id);
        await organizationRepository.Received(1).DeleteAsync(organization);
        await applicationCacheService.Received(1).DeleteOrganizationAbilityAsync(organization.Id);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_Fails_KeyConnector(Organization organization, SutProvider<OrganizationDeleteCommand> sutProvider,
        SsoConfig ssoConfig)
    {
        ssoConfig.Enabled = true;
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });
        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

        ssoConfigRepository.GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(organization));

        Assert.Contains("You cannot delete an Organization that is using Key Connector.", exception.Message);

        await organizationRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
        await applicationCacheService.DidNotReceiveWithAnyArgs().DeleteOrganizationAbilityAsync(default);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_WhenFlagEnabled_CallsSubscriberService(
        Organization organization,
        SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.ExpirationDate = DateTime.UtcNow.AddDays(10);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);

        await sutProvider.Sut.DeleteAsync(organization);

        await sutProvider.GetDependency<ISubscriberService>()
            .Received(1)
            .CancelSubscription(organization, cancelImmediately: false);

        await sutProvider.GetDependency<IStripePaymentService>()
            .DidNotReceiveWithAnyArgs()
            .CancelSubscriptionAsync(default, default);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_WhenFlagDisabled_CallsLegacyPaymentService(
        Organization organization,
        SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.ExpirationDate = DateTime.UtcNow.AddDays(10);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(false);

        await sutProvider.Sut.DeleteAsync(organization);

        await sutProvider.GetDependency<IStripePaymentService>()
            .Received(1)
            .CancelSubscriptionAsync(organization, true);

        await sutProvider.GetDependency<ISubscriberService>()
            .DidNotReceiveWithAnyArgs()
            .CancelSubscription(default, default, default);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_WhenFlagEnabled_HandlesBillingException(
        Organization organization,
        SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.ExpirationDate = DateTime.UtcNow.AddDays(10);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(true);

        var billingException = new BillingException();
        sutProvider.GetDependency<ISubscriberService>()
            .CancelSubscription(organization, cancelImmediately: false)
            .ThrowsAsync(billingException);

        await sutProvider.Sut.DeleteAsync(organization);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).DeleteAsync(organization);

    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_WhenFlagDisabled_HandlesBillingException(
        Organization organization,
        SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = "sub_123";
        organization.ExpirationDate = DateTime.UtcNow.AddDays(10);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal)
            .Returns(false);

        var billingException = new BillingException();
        sutProvider.GetDependency<IStripePaymentService>()
            .CancelSubscriptionAsync(organization, Arg.Any<bool>())
            .ThrowsAsync(billingException);

        await sutProvider.Sut.DeleteAsync(organization);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).DeleteAsync(organization);
    }
}
