using System.Text.Json;
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
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
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
    public async Task Delete_Success(Organization organization, SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

        await sutProvider.Sut.DeleteAsync(organization);

        await organizationRepository.Received().DeleteAsync(organization);
        await applicationCacheService.Received().DeleteOrganizationAbilityAsync(organization.Id);
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

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_WithFileSends_DeletesFilesBeforeDbRecords(
        Organization organization,
        SutProvider<OrganizationDeleteCommand> sutProvider)
    {
        // Ensuring that the file is deleted first avoids the following situation:
        // 1. DB row is deleted successfully
        // 2. File blob fails to delete
        // 3. File blob still exists but with no parent Send
        var fileData = new SendFileData { Id = "file1", FileName = "test.txt", Size = 100 };
        var fileSend = new Send
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Type = SendType.File,
            Data = JsonSerializer.Serialize(fileData)
        };
        var textSend = new Send
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Type = SendType.Text,
            Data = "{}"
        };

        sutProvider.GetDependency<ISendRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Send> { fileSend, textSend });

        var callOrder = new List<string>();
        sutProvider.GetDependency<ISendFileStorageService>()
            .DeleteFileAsync(fileSend, fileData.Id)
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("file"));
        sutProvider.GetDependency<IOrganizationRepository>()
            .DeleteAsync(organization)
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("db"));

        await sutProvider.Sut.DeleteAsync(organization);

        await sutProvider.GetDependency<ISendFileStorageService>()
            .Received(1).DeleteFileAsync(fileSend, fileData.Id);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1).DeleteAsync(organization);
        Assert.Equal(new[] { "file", "db" }, callOrder);
    }
}
