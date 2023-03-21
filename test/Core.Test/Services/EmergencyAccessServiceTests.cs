using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class EmergencyAccessServiceTests
{
    [Theory, BitAutoData]
    public async Task SaveAsync_PremiumCannotUpdate(
        SutProvider<EmergencyAccessService> sutProvider, User savingUser)
    {
        savingUser.Premium = false;
        var emergencyAccess = new EmergencyAccess
        {
            Type = Enums.EmergencyAccessType.Takeover,
            GrantorId = savingUser.Id,
        };

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(savingUser.Id).Returns(savingUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(emergencyAccess, savingUser));

        Assert.Contains("Not a premium user.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InviteAsync_UserWithKeyConnectorCannotUseTakeover(
        SutProvider<EmergencyAccessService> sutProvider, User invitingUser, string email, int waitTime)
    {
        invitingUser.UsesKeyConnector = true;
        sutProvider.GetDependency<IUserService>().CanAccessPremium(invitingUser).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteAsync(invitingUser, email, Enums.EmergencyAccessType.Takeover, waitTime));

        Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_UserWithKeyConnectorCannotUseTakeover(
        SutProvider<EmergencyAccessService> sutProvider, User confirmingUser, string key)
    {
        confirmingUser.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Status = Enums.EmergencyAccessStatusType.Accepted,
            GrantorId = confirmingUser.Id,
            Type = Enums.EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(confirmingUser.Id).Returns(confirmingUser);
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(new Guid(), key, confirmingUser.Id));

        Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_UserWithKeyConnectorCannotUseTakeover(
        SutProvider<EmergencyAccessService> sutProvider, User savingUser)
    {
        savingUser.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Type = Enums.EmergencyAccessType.Takeover,
            GrantorId = savingUser.Id,
        };

        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByIdAsync(savingUser.Id).Returns(savingUser);
        userService.CanAccessPremium(savingUser).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(emergencyAccess, savingUser));

        Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_UserWithKeyConnectorCannotUseTakeover(
        SutProvider<EmergencyAccessService> sutProvider, User initiatingUser, User grantor)
    {
        grantor.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Status = Enums.EmergencyAccessStatusType.Confirmed,
            GranteeId = initiatingUser.Id,
            GrantorId = grantor.Id,
            Type = Enums.EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(grantor.Id).Returns(grantor);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser));

        Assert.Contains("You cannot takeover an account that is using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task TakeoverAsync_UserWithKeyConnectorCannotUseTakeover(
        SutProvider<EmergencyAccessService> sutProvider, User requestingUser, User grantor)
    {
        grantor.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantor.Id,
            GranteeId = requestingUser.Id,
            Status = Enums.EmergencyAccessStatusType.RecoveryApproved,
            Type = Enums.EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(grantor.Id).Returns(grantor);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TakeoverAsync(new Guid(), requestingUser));

        Assert.Contains("You cannot takeover an account that is using Key Connector", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_Disables_2FA_Providers_On_The_Grantor(
        SutProvider<EmergencyAccessService> sutProvider, User requestingUser, User grantor)
    {
        grantor.UsesKeyConnector = true;
        grantor.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = "asdfasf" },
                Enabled = true
            }
        });
        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantor.Id,
            GranteeId = requestingUser.Id,
            Status = Enums.EmergencyAccessStatusType.RecoveryApproved,
            Type = Enums.EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(grantor.Id).Returns(grantor);

        await sutProvider.Sut.PasswordAsync(Guid.NewGuid(), requestingUser, "blablahash", "blablakey");

        Assert.Empty(grantor.GetTwoFactorProviders());
        await sutProvider.GetDependency<IUserRepository>().Received().ReplaceAsync(grantor);
    }
}
