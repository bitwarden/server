using Bit.Commercial.Core.AdminConsole.Providers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.AdminConsole.ProviderFeatures;

[SutProviderCustomize]
public class CreateProviderCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateMspAsync_UserIdIsInvalid_Throws(Provider provider, SutProvider<CreateProviderCommand> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateMspAsync(provider, default, default, default));

        // Assert
        Assert.Contains("Invalid owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CreateMspAsync_Success(Provider provider, User user, SutProvider<CreateProviderCommand> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        var userRepository = sutProvider.GetDependency<IUserRepository>();
        userRepository.GetByEmailAsync(user.Email).Returns(user);

        // Act
        await sutProvider.Sut.CreateMspAsync(provider, user.Email, default, default);

        // Assert
        await sutProvider.GetDependency<IProviderRepository>().ReceivedWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IProviderService>().Received(1).SendProviderSetupInviteEmailAsync(provider, user.Email);
    }

    [Theory, BitAutoData]
    public async Task CreateResellerAsync_Success(Provider provider, SutProvider<CreateProviderCommand> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Reseller;

        // Act
        await sutProvider.Sut.CreateResellerAsync(provider);

        // Assert
        await sutProvider.GetDependency<IProviderRepository>().ReceivedWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IProviderService>().DidNotReceiveWithAnyArgs().SendProviderSetupInviteEmailAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CreateMultiOrganizationEnterpriseAsync_Success(
    Provider provider,
    User user,
    PlanType plan,
    int minimumSeats,
    SutProvider<CreateProviderCommand> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.MultiOrganizationEnterprise;

        var userRepository = sutProvider.GetDependency<IUserRepository>();
        userRepository.GetByEmailAsync(user.Email).Returns(user);

        // Act
        await sutProvider.Sut.CreateMultiOrganizationEnterpriseAsync(provider, user.Email, plan, minimumSeats);

        // Assert
        await sutProvider.GetDependency<IProviderRepository>().ReceivedWithAnyArgs().CreateAsync(provider);
        await sutProvider.GetDependency<IProviderService>().Received(1).SendProviderSetupInviteEmailAsync(provider, user.Email);
    }

    [Theory, BitAutoData]
    public async Task CreateMultiOrganizationEnterpriseAsync_UserIdIsInvalid_Throws(
        Provider provider,
        SutProvider<CreateProviderCommand> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateMultiOrganizationEnterpriseAsync(provider, default, default, default));

        // Assert
        Assert.Contains("Invalid owner.", exception.Message);
    }
}
