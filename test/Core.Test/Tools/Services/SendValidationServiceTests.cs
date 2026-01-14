using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

[SutProviderCustomize]
public class SendValidationServiceTests
{
    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_OrgGrantedPremiumUser_UsesPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        User user)
    {
        // Arrange
        send.UserId = user.Id;
        send.OrganizationId = null;
        send.Type = SendType.File;
        user.Premium = false;
        user.Storage = 1024L * 1024L * 1024L; // 1 GB used
        user.EmailVerified = true;

        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        var premiumPlan = new Plan
        {
            Storage = new Purchasable { Provided = 5 }
        };
        sutProvider.GetDependency<IPricingClient>().GetAvailablePremiumPlan().Returns(premiumPlan);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert
        await sutProvider.GetDependency<IPricingClient>().Received(1).GetAvailablePremiumPlan();
        Assert.True(result > 0);
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_IndividualPremium_DoesNotCallPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        User user)
    {
        // Arrange
        send.UserId = user.Id;
        send.OrganizationId = null;
        send.Type = SendType.File;
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.EmailVerified = true;

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert - should NOT call pricing service for individual premium users
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_SelfHosted_DoesNotCallPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        User user)
    {
        // Arrange
        send.UserId = user.Id;
        send.OrganizationId = null;
        send.Type = SendType.File;
        user.Premium = false;
        user.EmailVerified = true;

        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert - should NOT call pricing service for self-hosted
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_OrgSend_DoesNotCallPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        Organization org)
    {
        // Arrange
        send.UserId = null;
        send.OrganizationId = org.Id;
        send.Type = SendType.File;
        org.MaxStorageGb = 100;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert - should NOT call pricing service for org sends
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }
}
