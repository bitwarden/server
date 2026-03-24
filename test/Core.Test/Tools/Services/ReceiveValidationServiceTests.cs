using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

[SutProviderCustomize]
public class ReceiveValidationServiceTests
{
    [Theory, BitAutoData]
    public void ValidateUpload_UserIdIsNull_ThrowsBadRequest(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive)
    {
        receive.UserId = null;

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateUpload(receive));
        Assert.Contains("Invalid Receive owner", exception.Message);
    }

    [Theory, BitAutoData]
    public void ValidateUpload_UserIdHasValue_DoesNotThrow(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive)
    {
        receive.UserId = Guid.NewGuid();

        // No exception implies success
        sutProvider.Sut.ValidateUpload(receive);
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForReceiveAsync_UserNotFound_ThrowsBadRequest(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive)
    {
        receive.UserId = Guid.NewGuid();

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(receive.UserId.Value)
            .Returns((User)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.StorageRemainingForReceiveAsync(receive));
        Assert.Contains("Invalid Receive Owner", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForReceiveAsync_UserCannotAccessPremium_ThrowsBadRequest(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive,
        User user)
    {
        receive.UserId = user.Id;

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.StorageRemainingForReceiveAsync(receive));
        Assert.Contains("does not have a Premium Subscription", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForReceiveAsync_EmailNotVerified_ThrowsBadRequest(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive,
        User user)
    {
        receive.UserId = user.Id;
        user.EmailVerified = false;

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.StorageRemainingForReceiveAsync(receive));
        Assert.Contains("has not verified their email", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForReceiveAsync_IndividualPremium_UsesUserStorage(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive,
        User user)
    {
        receive.UserId = user.Id;
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.EmailVerified = true;

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        var result = await sutProvider.Sut.StorageRemainingForReceiveAsync(receive);

        // Should NOT call pricing service for individual premium users
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForReceiveAsync_OrgGrantedPremium_UsesPricingService(
        SutProvider<ReceiveValidationService> sutProvider,
        Receive receive,
        User user)
    {
        receive.UserId = user.Id;
        user.Premium = false;
        user.Storage = 1024L * 1024L * 1024L; // 1 GB used
        user.EmailVerified = true;

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        var premiumPlan = new Plan
        {
            Storage = new Purchasable { Provided = 5 }
        };
        sutProvider.GetDependency<IPricingClient>().GetAvailablePremiumPlan().Returns(premiumPlan);

        var result = await sutProvider.Sut.StorageRemainingForReceiveAsync(receive);

        await sutProvider.GetDependency<IPricingClient>().Received(1).GetAvailablePremiumPlan();
        Assert.True(result > 0);
    }
}
