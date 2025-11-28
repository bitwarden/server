using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class EmergencyAccessServiceTests
{
    [Theory, BitAutoData]
    public async Task InviteAsync_UserWithOutPremium_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User invitingUser, string email, int waitTime)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(invitingUser).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteAsync(invitingUser, email, EmergencyAccessType.Takeover, waitTime));

        Assert.Contains("Not a premium user.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
                        .DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InviteAsync_UserWithKeyConnector_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User invitingUser, string email, int waitTime)
    {
        invitingUser.UsesKeyConnector = true;
        sutProvider.GetDependency<IUserService>().CanAccessPremium(invitingUser).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteAsync(invitingUser, email, EmergencyAccessType.Takeover, waitTime));

        Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
                        .DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Theory]
    [BitAutoData(EmergencyAccessType.Takeover)]
    [BitAutoData(EmergencyAccessType.View)]
    public async Task InviteAsync_ReturnsEmergencyAccessObject(
        EmergencyAccessType accessType, SutProvider<EmergencyAccessService> sutProvider, User invitingUser, string email, int waitTime)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(invitingUser).Returns(true);

        var result = await sutProvider.Sut.InviteAsync(invitingUser, email, accessType, waitTime);

        Assert.NotNull(result);
        Assert.Equal(accessType, result.Type);
        Assert.Equal(invitingUser.Id, result.GrantorId);
        Assert.Equal(email, result.Email);
        Assert.Equal(EmergencyAccessStatusType.Invited, result.Status);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
                         .Received(1)
                         .CreateAsync(Arg.Any<EmergencyAccess>());
        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
                   .Received(1)
                   .Protect(Arg.Any<EmergencyAccessInviteTokenable>());
        await sutProvider.GetDependency<IMailService>()
                         .Received(1)
                         .SendEmergencyAccessInviteEmailAsync(Arg.Any<EmergencyAccess>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task GetAsync_EmergencyAccessNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User user)
    {
        EmergencyAccessDetails emergencyAccess = null;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetDetailsByIdGrantorIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
                .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetAsync(new Guid(), user.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ResendInviteAsync_EmergencyAccessNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        Guid emergencyAccessId)
    {
        EmergencyAccess emergencyAccess = null;

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ResendInviteAsync(invitingUser, emergencyAccessId));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IMailService>()
                .DidNotReceiveWithAnyArgs()
                .SendEmergencyAccessInviteEmailAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task ResendInviteAsync_InvitingUserIdNotGrantorUserId_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        Guid emergencyAccessId)
    {
        var emergencyAccess = new EmergencyAccess
        {
            Status = EmergencyAccessStatusType.Invited,
            GrantorId = Guid.NewGuid(),
            Type = EmergencyAccessType.Takeover,
        }; ;

        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ResendInviteAsync(invitingUser, emergencyAccessId));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IMailService>()
                         .DidNotReceiveWithAnyArgs()
                         .SendEmergencyAccessInviteEmailAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryApproved)]
    public async Task ResendInviteAsync_EmergencyAccessStatusInvalid_ThrowsBadRequest(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        Guid emergencyAccessId)
    {
        var emergencyAccess = new EmergencyAccess
        {
            Status = statusType,
            GrantorId = invitingUser.Id,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ResendInviteAsync(invitingUser, emergencyAccessId));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IMailService>()
                         .DidNotReceiveWithAnyArgs()
                         .SendEmergencyAccessInviteEmailAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task ResendInviteAsync_SendsInviteAsync(
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        Guid emergencyAccessId)
    {
        var emergencyAccess = new EmergencyAccess
        {
            Status = EmergencyAccessStatusType.Invited,
            GrantorId = invitingUser.Id,
            Type = EmergencyAccessType.Takeover,
        }; ;

        sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);

        await sutProvider.Sut.ResendInviteAsync(invitingUser, emergencyAccessId);
        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
                         .Received(1)
                         .Protect(Arg.Any<EmergencyAccessInviteTokenable>());
        await sutProvider.GetDependency<IMailService>()
                         .Received(1)
                         .SendEmergencyAccessInviteEmailAsync(emergencyAccess, invitingUser.Name, Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_EmergencyAccessNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User acceptingUser, string token)
    {
        EmergencyAccess emergencyAccess = null;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(new Guid(), acceptingUser, token, sutProvider.GetDependency<IUserService>()));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_CannotUnprotectToken_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User acceptingUser,
        EmergencyAccess emergencyAccess,
        string token)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
        .TryUnprotect(token, out Arg.Any<EmergencyAccessInviteTokenable>())
        .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(emergencyAccess.Id, acceptingUser, token, sutProvider.GetDependency<IUserService>()));

        Assert.Contains("Invalid token.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_TokenDataInvalid_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User acceptingUser,
        EmergencyAccess emergencyAccess,
        EmergencyAccess wrongEmergencyAccess,
        string token)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(token, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
                {
                    callInfo[1] = new EmergencyAccessInviteTokenable(wrongEmergencyAccess, 1);
                    return true;
                });


        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(emergencyAccess.Id, acceptingUser, token, sutProvider.GetDependency<IUserService>()));

        Assert.Contains("Invalid token.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_AcceptedStatus_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User acceptingUser,
        EmergencyAccess emergencyAccess,
        string token)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.Accepted;
        emergencyAccess.Email = acceptingUser.Email;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(token, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 1);
                return true;
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(emergencyAccess.Id, acceptingUser, token, sutProvider.GetDependency<IUserService>()));

        Assert.Contains("Invitation already accepted. You will receive an email when the grantor confirms you as an emergency access contact.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_NotInvitedStatus_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User acceptingUser,
        EmergencyAccess emergencyAccess,
        string token)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.Confirmed;
        emergencyAccess.Email = acceptingUser.Email;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(token, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 1);
                return true;
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(emergencyAccess.Id, acceptingUser, token, sutProvider.GetDependency<IUserService>()));

        Assert.Contains("Invitation already accepted.", exception.Message);
    }

    [Theory(Skip = "Code not reachable, Tokenable checks email match in IsValid()"), BitAutoData]
    public async Task AcceptUserAsync_EmergencyAccessEmailDoesNotMatch_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User acceptingUser,
        EmergencyAccess emergencyAccess,
        string token)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.Invited;
        emergencyAccess.Email = acceptingUser.Email;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(token, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 1);
                return true;
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(emergencyAccess.Id, acceptingUser, token, sutProvider.GetDependency<IUserService>()));

        Assert.Contains("User email does not match invite.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_ReplaceEmergencyAccess_SendsEmail_Success(
        SutProvider<EmergencyAccessService> sutProvider,
        User acceptingUser,
        User invitingUser,
        EmergencyAccess emergencyAccess,
        string token)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.Invited;
        emergencyAccess.Email = acceptingUser.Email;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserService>()
                .GetUserByIdAsync(Arg.Any<Guid>())
                .Returns(invitingUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(token, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 1);
                return true;
            });

        await sutProvider.Sut.AcceptUserAsync(emergencyAccess.Id, acceptingUser, token, sutProvider.GetDependency<IUserService>());

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
                .Received(1)
                .ReplaceAsync(Arg.Is<EmergencyAccess>(x => x.Status == EmergencyAccessStatusType.Accepted));

        await sutProvider.GetDependency<IMailService>()
                .Received(1)
                .SendEmergencyAccessAcceptedEmailAsync(acceptingUser.Email, invitingUser.Email);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmergencyAccessNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        EmergencyAccess emergencyAccess)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(emergencyAccess.Id, invitingUser.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmergencyAccessGrantorIdNotEqual_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        EmergencyAccess emergencyAccess)
    {
        emergencyAccess.GrantorId = Guid.NewGuid();
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(emergencyAccess.Id, invitingUser.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmergencyAccessGranteeIdNotEqual_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User invitingUser,
        EmergencyAccess emergencyAccess)
    {
        emergencyAccess.GranteeId = Guid.NewGuid();
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(emergencyAccess.Id, invitingUser.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_EmergencyAccessIsDeleted_Success(
    SutProvider<EmergencyAccessService> sutProvider,
    User user,
    EmergencyAccess emergencyAccess)
    {
        emergencyAccess.GranteeId = user.Id;
        emergencyAccess.GrantorId = user.Id;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        await sutProvider.Sut.DeleteAsync(emergencyAccess.Id, user.Id);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
                .Received(1)
                .DeleteAsync(emergencyAccess);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_EmergencyAccessNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        string key,
        User grantorUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(emergencyAccess.Id, key, grantorUser.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_EmergencyAccessStatusIsNotAccepted_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        string key,
        User grantorUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(emergencyAccess.Id)
                .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(emergencyAccess.Id, key, grantorUser.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);

    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_EmergencyAccessGrantorIdNotEqualToConfirmingUserId_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        string key,
        User grantorUser)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(emergencyAccess.Id, key, grantorUser.Id));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_UserWithKeyConnectorCannotUseTakeover_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User confirmingUser, string key)
    {
        confirmingUser.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Status = EmergencyAccessStatusType.Accepted,
            GrantorId = confirmingUser.Id,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(confirmingUser.Id)
            .Returns(confirmingUser);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(new Guid(), key, confirmingUser.Id));

        Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_ConfirmsAndReplacesEmergencyAccess_Success(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        string key,
        User grantorUser,
        User granteeUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.Accepted;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
                .GetByIdAsync(Arg.Any<Guid>())
                .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantorUser.Id)
            .Returns(grantorUser);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GranteeId.Value)
            .Returns(granteeUser);

        await sutProvider.Sut.ConfirmUserAsync(emergencyAccess.Id, key, grantorUser.Id);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
                .Received(1)
                .ReplaceAsync(Arg.Is<EmergencyAccess>(x => x.Status == EmergencyAccessStatusType.Confirmed));

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendEmergencyAccessConfirmedEmailAsync(grantorUser.Name, granteeUser.Email);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_PremiumCannotUpdate_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User savingUser)
    {
        var emergencyAccess = new EmergencyAccess
        {
            Type = EmergencyAccessType.Takeover,
            GrantorId = savingUser.Id,
        };

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(savingUser)
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(emergencyAccess, savingUser));

        Assert.Contains("Not a premium user.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_EmergencyAccessGrantorIdNotEqualToSavingUserId_ThrowsBadRequest(
    SutProvider<EmergencyAccessService> sutProvider, User savingUser)
    {
        savingUser.Premium = true;
        var emergencyAccess = new EmergencyAccess
        {
            Type = EmergencyAccessType.Takeover,
            GrantorId = new Guid(),
        };

        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(savingUser.Id)
            .Returns(savingUser);
        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(savingUser)
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(emergencyAccess, savingUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_GrantorUserWithKeyConnectorCannotTakeover_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User grantorUser)
    {
        grantorUser.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Type = EmergencyAccessType.Takeover,
            GrantorId = grantorUser.Id,
        };

        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByIdAsync(grantorUser.Id).Returns(grantorUser);
        userService.CanAccessPremium(grantorUser).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(emergencyAccess, grantorUser));

        Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_GrantorUserWithKeyConnectorCanView_SavesEmergencyAccess(
        SutProvider<EmergencyAccessService> sutProvider, User grantorUser)
    {
        grantorUser.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Type = EmergencyAccessType.View,
            GrantorId = grantorUser.Id,
        };

        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByIdAsync(grantorUser.Id).Returns(grantorUser);
        userService.CanAccessPremium(grantorUser).Returns(true);

        await sutProvider.Sut.SaveAsync(emergencyAccess, grantorUser);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .ReplaceAsync(emergencyAccess);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ValidRequest_SavesEmergencyAccess(
        SutProvider<EmergencyAccessService> sutProvider, User grantorUser)
    {
        grantorUser.UsesKeyConnector = false;
        var emergencyAccess = new EmergencyAccess
        {
            Type = EmergencyAccessType.Takeover,
            GrantorId = grantorUser.Id,
        };

        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByIdAsync(grantorUser.Id).Returns(grantorUser);
        userService.CanAccessPremium(grantorUser).Returns(true);

        await sutProvider.Sut.SaveAsync(emergencyAccess, grantorUser);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .ReplaceAsync(emergencyAccess);
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_EmergencyAccessNull_ThrowBadRequest(
    SutProvider<EmergencyAccessService> sutProvider, User initiatingUser)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_EmergencyAccessGranteeIdNotEqual_ThrowBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User initiatingUser)
    {
        emergencyAccess.GranteeId = new Guid();
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(emergencyAccess.Id)
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_EmergencyAccessStatusIsNotConfirmed_ThrowBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User initiatingUser)
    {
        emergencyAccess.GranteeId = initiatingUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.Invited;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(emergencyAccess.Id)
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_UserWithKeyConnectorCannotUseTakeover_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider, User initiatingUser, User grantor)
    {
        grantor.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Status = EmergencyAccessStatusType.Confirmed,
            GranteeId = initiatingUser.Id,
            GrantorId = grantor.Id,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantor.Id)
            .Returns(grantor);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser));

        Assert.Contains("You cannot takeover an account that is using Key Connector", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_UserWithKeyConnectorCanView_Success(
        SutProvider<EmergencyAccessService> sutProvider, User initiatingUser, User grantor)
    {
        grantor.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            Status = EmergencyAccessStatusType.Confirmed,
            GranteeId = initiatingUser.Id,
            GrantorId = grantor.Id,
            Type = EmergencyAccessType.View,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantor.Id)
            .Returns(grantor);

        await sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<EmergencyAccess>(x => x.Status == EmergencyAccessStatusType.RecoveryInitiated));
    }

    [Theory, BitAutoData]
    public async Task InitiateAsync_RequestIsCorrect_Success(
        SutProvider<EmergencyAccessService> sutProvider, User initiatingUser, User grantor)
    {
        var emergencyAccess = new EmergencyAccess
        {
            Status = EmergencyAccessStatusType.Confirmed,
            GranteeId = initiatingUser.Id,
            GrantorId = grantor.Id,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantor.Id)
            .Returns(grantor);

        await sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<EmergencyAccess>(x => x.Status == EmergencyAccessStatusType.RecoveryInitiated));
    }

    [Theory, BitAutoData]
    public async Task ApproveAsync_EmergencyAccessNull_ThrowsBadrequest(
        SutProvider<EmergencyAccessService> sutProvider)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ApproveAsync(new Guid(), null));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ApproveAsync_EmergencyAccessGrantorIdNotEquatToApproving_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User grantorUser)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ApproveAsync(emergencyAccess.Id, grantorUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Invited)]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryApproved)]
    public async Task ApproveAsync_EmergencyAccessStatusNotRecoveryInitiated_ThrowsBadRequest(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User grantorUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = statusType;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ApproveAsync(emergencyAccess.Id, grantorUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ApproveAsync_Success(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User grantorUser,
        User granteeUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryInitiated;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(granteeUser);

        await sutProvider.Sut.ApproveAsync(emergencyAccess.Id, grantorUser);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<EmergencyAccess>(x => x.Status == EmergencyAccessStatusType.RecoveryApproved));
    }

    [Theory, BitAutoData]
    public async Task RejectAsync_EmergencyAccessIdNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User GrantorUser)
    {
        emergencyAccess.GrantorId = GrantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.Accepted;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RejectAsync(emergencyAccess.Id, GrantorUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RejectAsync_EmergencyAccessGrantorIdNotEqualToRequestUser_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User GrantorUser)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.Accepted;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RejectAsync(emergencyAccess.Id, GrantorUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Invited)]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    public async Task RejectAsync_EmergencyAccessStatusNotValid_ThrowsBadRequest(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User GrantorUser)
    {
        emergencyAccess.GrantorId = GrantorUser.Id;
        emergencyAccess.Status = statusType;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RejectAsync(emergencyAccess.Id, GrantorUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryApproved)]
    public async Task RejectAsync_Success(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User GrantorUser,
        User GranteeUser)
    {
        emergencyAccess.GrantorId = GrantorUser.Id;
        emergencyAccess.Status = statusType;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(GranteeUser);

        await sutProvider.Sut.RejectAsync(emergencyAccess.Id, GrantorUser);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<EmergencyAccess>(x => x.Status == EmergencyAccessStatusType.Confirmed));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_RequestNotValidEmergencyAccessNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetPoliciesAsync(default, default));
        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Invited)]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task GetPoliciesAsync_RequestNotValidStatusType_ThrowsBadRequest(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = statusType;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetPoliciesAsync(emergencyAccess.Id, granteeUser));
        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_RequestNotValidType_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.View;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetPoliciesAsync(emergencyAccess.Id, granteeUser));
        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task GetPoliciesAsync_OrganizationUserTypeNotOwner_ReturnsNull(
        OrganizationUserType userType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser,
        User grantorUser,
        OrganizationUser grantorOrganizationUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GrantorId)
            .Returns(grantorUser);

        grantorOrganizationUser.UserId = grantorUser.Id;
        grantorOrganizationUser.Type = userType;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(grantorUser.Id)
            .Returns([grantorOrganizationUser]);

        var result = await sutProvider.Sut.GetPoliciesAsync(emergencyAccess.Id, granteeUser);
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_OrganizationUserEmpty_ReturnsNull(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser,
        User grantorUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GrantorId)
            .Returns(grantorUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(grantorUser.Id)
            .Returns([]);


        var result = await sutProvider.Sut.GetPoliciesAsync(emergencyAccess.Id, granteeUser);
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_ReturnsNotNull(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser,
        User grantorUser,
        OrganizationUser grantorOrganizationUser)
    {
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GrantorId)
            .Returns(grantorUser);

        grantorOrganizationUser.UserId = grantorUser.Id;
        grantorOrganizationUser.Type = OrganizationUserType.Owner;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(grantorUser.Id)
            .Returns([grantorOrganizationUser]);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByUserIdAsync(grantorUser.Id)
            .Returns([]);

        var result = await sutProvider.Sut.GetPoliciesAsync(emergencyAccess.Id, granteeUser);
        Assert.NotNull(result);
    }

    [Theory, BitAutoData]
    public async Task TakeoverAsync_RequestNotValid_EmergencyAccessIsNull_ThrowsBadRequest(
    SutProvider<EmergencyAccessService> sutProvider)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TakeoverAsync(default, default));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task TakeoverAsync_RequestNotValid_GranteeNotEqualToRequestingUser_ThrowsBadRequest(
    SutProvider<EmergencyAccessService> sutProvider,
    EmergencyAccess emergencyAccess,
    User granteeUser)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TakeoverAsync(new Guid(), granteeUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Invited)]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task TakeoverAsync_RequestNotValid_StatusType_ThrowsBadRequest(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = statusType;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TakeoverAsync(new Guid(), granteeUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task TakeoverAsync_RequestNotValid_TypeIsView_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.View;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TakeoverAsync(new Guid(), granteeUser));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task TakeoverAsync_UserWithKeyConnectorCannotUseTakeover_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        User granteeUser,
        User grantor)
    {
        grantor.UsesKeyConnector = true;
        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantor.Id,
            GranteeId = granteeUser.Id,
            Status = EmergencyAccessStatusType.RecoveryApproved,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantor.Id)
            .Returns(grantor);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.TakeoverAsync(new Guid(), granteeUser));

        Assert.Contains("You cannot takeover an account that is using Key Connector", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task TakeoverAsync_Success_ReturnsEmergencyAccessAndGrantorUser(
    SutProvider<EmergencyAccessService> sutProvider,
    User granteeUser,
    User grantor)
    {
        grantor.UsesKeyConnector = false;
        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantor.Id,
            GranteeId = granteeUser.Id,
            Status = EmergencyAccessStatusType.RecoveryApproved,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantor.Id)
            .Returns(grantor);

        var result = await sutProvider.Sut.TakeoverAsync(new Guid(), granteeUser);

        Assert.Equal(result.Item1, emergencyAccess);
        Assert.Equal(result.Item2, grantor);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_RequestNotValid_EmergencyAccessIsNull_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((EmergencyAccess)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PasswordAsync(default, default, default, default));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_RequestNotValid_GranteeNotEqualToRequestingUser_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PasswordAsync(emergencyAccess.Id, granteeUser, default, default));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Invited)]
    [BitAutoData(EmergencyAccessStatusType.Accepted)]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task PasswordAsync_RequestNotValid_StatusType_ThrowsBadRequest(
        EmergencyAccessStatusType statusType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = statusType;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PasswordAsync(emergencyAccess.Id, granteeUser, default, default));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_RequestNotValid_TypeIsView_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.View;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PasswordAsync(emergencyAccess.Id, granteeUser, default, default));

        Assert.Contains("Emergency Access not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_NonOrgUser_Success(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser,
        User grantorUser,
        string key,
        string passwordHash)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GrantorId)
            .Returns(grantorUser);

        await sutProvider.Sut.PasswordAsync(emergencyAccess.Id, granteeUser, passwordHash, key);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdatePasswordHash(grantorUser, passwordHash);
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.VerifyDevices == false && u.Key == key));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task PasswordAsync_OrgUser_NotOrganizationOwner_RemovedFromOrganization_Success(
        OrganizationUserType userType,
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser,
        User grantorUser,
        OrganizationUser organizationUser,
        string key,
        string passwordHash)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GrantorId)
            .Returns(grantorUser);

        organizationUser.UserId = grantorUser.Id;
        organizationUser.Type = userType;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(grantorUser.Id)
            .Returns([organizationUser]);

        await sutProvider.Sut.PasswordAsync(emergencyAccess.Id, granteeUser, passwordHash, key);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdatePasswordHash(grantorUser, passwordHash);
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.VerifyDevices == false && u.Key == key));
        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>()
            .Received(1)
            .RemoveUserAsync(organizationUser.OrganizationId, organizationUser.UserId.Value);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_OrgUser_IsOrganizationOwner_NotRemovedFromOrganization_Success(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser,
        User grantorUser,
        OrganizationUser organizationUser,
        string key,
        string passwordHash)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.GrantorId = grantorUser.Id;
        emergencyAccess.Status = EmergencyAccessStatusType.RecoveryApproved;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(emergencyAccess.GrantorId)
            .Returns(grantorUser);

        organizationUser.UserId = grantorUser.Id;
        organizationUser.Type = OrganizationUserType.Owner;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(grantorUser.Id)
            .Returns([organizationUser]);

        await sutProvider.Sut.PasswordAsync(emergencyAccess.Id, granteeUser, passwordHash, key);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdatePasswordHash(grantorUser, passwordHash);
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.VerifyDevices == false && u.Key == key));
        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>()
            .Received(0)
            .RemoveUserAsync(organizationUser.OrganizationId, organizationUser.UserId.Value);
    }

    [Theory, BitAutoData]
    public async Task PasswordAsync_Disables_NewDeviceVerification_And_TwoFactorProviders_On_The_Grantor(
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
            Status = EmergencyAccessStatusType.RecoveryApproved,
            Type = EmergencyAccessType.Takeover,
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(grantor.Id)
            .Returns(grantor);

        await sutProvider.Sut.PasswordAsync(Guid.NewGuid(), requestingUser, "blablahash", "blablakey");

        Assert.Empty(grantor.GetTwoFactorProviders());
        Assert.False(grantor.VerifyDevices);
        await sutProvider.GetDependency<IUserRepository>().Received().ReplaceAsync(grantor);
    }

    [Theory, BitAutoData]
    public async Task ViewAsync_EmergencyAccessTypeNotView_ThrowsBadRequest(
        SutProvider<EmergencyAccessService> sutProvider,
        EmergencyAccess emergencyAccess,
        User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ViewAsync(emergencyAccess.Id, granteeUser));
    }

    [Theory, BitAutoData]
    public async Task GetAttachmentDownloadAsync_EmergencyAccessTypeNotView_ThrowsBadRequest(
    SutProvider<EmergencyAccessService> sutProvider,
    EmergencyAccess emergencyAccess,
    User granteeUser)
    {
        emergencyAccess.GranteeId = granteeUser.Id;
        emergencyAccess.Type = EmergencyAccessType.Takeover;
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns(emergencyAccess);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetAttachmentDownloadAsync(emergencyAccess.Id, default, default, granteeUser));
    }
}
