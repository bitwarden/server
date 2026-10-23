using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Bit.Core.Auth.UserFeatures.DeviceTrust.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.DeviceTrust;

[SutProviderCustomize]
public class FinishSsoJitProvisionTrustedDeviceCommandTests
{
    private const string _resetPasswordKey = "4.YWJjZA==";
    private const string _userKeyId = "0123456789abcdef0123456789abcdef";

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_UserHasExistingKeypair_ThrowsBadRequestAndMakesNoWrites(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device device)
    {
        user.PublicKey = "existing-public-key";
        var data = CreateData(orgUser.OrganizationId, deviceIdentifier: device.Identifier);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true));

        Assert.Equal("User has existing keypair", exception.Message);
        await AssertNoWritesAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_OrganizationDoesNotUseTrustedDeviceEncryption_ThrowsBadRequestAndMakesNoWrites(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device device)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, deviceIdentifier: device.Identifier);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(orgUser.OrganizationId)
            .Returns(CreateSsoConfig(orgUser.OrganizationId, MemberDecryptionType.MasterPassword));

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true));

        await AssertNoWritesAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_OrganizationHasNoSsoConfig_ThrowsBadRequestAndMakesNoWrites(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device device)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, deviceIdentifier: device.Identifier);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(orgUser.OrganizationId)
            .ReturnsNull();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true));

        await AssertNoWritesAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_MemberNotFound_ThrowsBadRequestAndMakesNoWrites(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device device)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, deviceIdentifier: device.Identifier);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgUser.OrganizationId, user.Id)
            .ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: false));

        Assert.Equal("User not found within organization.", exception.Message);
        await AssertNoWritesAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_DeviceNotFound_ThrowsNotFoundAndMakesNoWrites(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device device)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, deviceIdentifier: device.Identifier);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device);
        sutProvider.GetDependency<IDeviceRepository>()
            .GetByIdentifierAsync(device.Identifier, user.Id)
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: false));

        await AssertNoWritesAsync(sutProvider);
    }

    [Theory]
    [BitAutoData((string?)null)]
    [BitAutoData("")]
    public async Task FinishProvisionAsync_AcceptInviteWithoutResetPasswordKey_ThrowsBadRequestAndMakesNoWrites(
        string? resetPasswordKey,
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, resetPasswordKey: resetPasswordKey);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true));

        await AssertNoWritesAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_OpenInvite_StoresKeysAndSkipsEnrollmentAndAccept(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        orgUser.Status = OrganizationUserStatusType.Invited;
        var data = CreateData(orgUser.OrganizationId);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: false);

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .SetV2AccountCryptographicStateAsync(user.Id, data.AccountKeys,
                Arg.Any<IEnumerable<UpdateUserData>?>());
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>().DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(default, default, default, default);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgIdAsync(default, default!, default!);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceiveWithAnyArgs()
            .AcceptOrgUserAsync(default!, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_DirectInviteAndMemberInvited_EnrollsThenAccepts(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        orgUser.Status = OrganizationUserStatusType.Invited;
        var data = CreateData(orgUser.OrganizationId);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true);

        Received.InOrder(() =>
        {
            sutProvider.GetDependency<IUserRepository>()
                .SetV2AccountCryptographicStateAsync(user.Id, data.AccountKeys,
                    Arg.Any<IEnumerable<UpdateUserData>?>());
            sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>()
                .UpdateUserResetPasswordEnrollmentAsync(orgUser.OrganizationId, user.Id, _resetPasswordKey, user.Id);
            sutProvider.GetDependency<IAcceptOrgUserCommand>()
                .AcceptOrgUserByOrgIdAsync(orgUser.OrganizationId, user, sutProvider.GetDependency<IUserService>());
        });
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_DirectInviteAndMemberAlreadyAccepted_EnrollsWithoutAccepting(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        orgUser.Status = OrganizationUserStatusType.Accepted;
        var data = CreateData(orgUser.OrganizationId);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true);

        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>().Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(orgUser.OrganizationId, user.Id, _resetPasswordKey, user.Id);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgIdAsync(default, default!, default!);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task FinishProvisionAsync_DeviceKeysPresent_TrustsDeviceForEveryInviteKind(
        bool acceptOrganizationInvite,
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device device)
    {
        ClearKeypair(user);
        device.EncryptedUserKey = null;
        device.EncryptedPublicKey = null;
        device.EncryptedPrivateKey = null;
        var data = CreateData(orgUser.OrganizationId, deviceIdentifier: device.Identifier);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite);

        await sutProvider.GetDependency<IDeviceService>().Received(1)
            .SaveAsync(Arg.Is<Device>(d =>
                d == device &&
                d.EncryptedUserKey == data.DeviceKeys!.EncryptedUserKey &&
                d.EncryptedPublicKey == data.DeviceKeys.EncryptedPublicKey &&
                d.EncryptedPrivateKey == data.DeviceKeys.EncryptedPrivateKey));
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_DeviceKeysAbsent_DoesNotTouchDevice(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId);
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: true);

        await sutProvider.GetDependency<IDeviceRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdentifierAsync(default!, default);
        await sutProvider.GetDependency<IDeviceService>().DidNotReceiveWithAnyArgs()
            .SaveAsync(default(Device)!);
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_V2KeysWithUserKeyId_SetsUserKeyIdInSameTransaction(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, userKeyId: KeyId.FromHexEncodedString(_userKeyId));
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);
        UpdateUserData setUserKeyId = (_, _) => Task.CompletedTask;
        sutProvider.GetDependency<IUserRepository>()
            .SetUserKeyId(user.Id, data.UserKeyId!)
            .Returns(setUserKeyId);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: false);

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .SetV2AccountCryptographicStateAsync(user.Id, data.AccountKeys,
                Arg.Is<IEnumerable<UpdateUserData>?>(actions =>
                    actions != null && actions.Single() == setUserKeyId));
    }

    [Theory, BitAutoData]
    public async Task FinishProvisionAsync_V1Keys_SavesKeypairOnUser(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser)
    {
        ClearKeypair(user);
        var data = CreateData(orgUser.OrganizationId, accountKeys: new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("2.wrapped|private|key", "public-key")
        });
        ArrangeValidPreconditions(sutProvider, user, orgUser, device: null);

        await sutProvider.Sut.FinishProvisionAsync(user, data, acceptOrganizationInvite: false);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .SaveUserAsync(Arg.Is<User>(u =>
                u == user && u.PublicKey == "public-key" && u.PrivateKey == "2.wrapped|private|key"));
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .SetV2AccountCryptographicStateAsync(default, default!, default);
    }

    private static void ClearKeypair(User user)
    {
        user.PublicKey = null;
        user.PrivateKey = null;
    }

    private static void ArrangeValidPreconditions(
        SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider,
        User user, OrganizationUser orgUser, Device? device)
    {
        orgUser.UserId = user.Id;
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(orgUser.OrganizationId)
            .Returns(CreateSsoConfig(orgUser.OrganizationId, MemberDecryptionType.TrustedDeviceEncryption));
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgUser.OrganizationId, user.Id)
            .Returns(orgUser);

        if (device != null)
        {
            device.UserId = user.Id;
            sutProvider.GetDependency<IDeviceRepository>()
                .GetByIdentifierAsync(device.Identifier, user.Id)
                .Returns(device);
        }
    }

    private static SsoConfig CreateSsoConfig(Guid organizationId, MemberDecryptionType memberDecryptionType)
    {
        var ssoConfig = new SsoConfig { OrganizationId = organizationId, Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = memberDecryptionType });
        return ssoConfig;
    }

    private static FinishSsoJitProvisionTrustedDeviceData CreateData(
        Guid organizationId,
        string? resetPasswordKey = _resetPasswordKey,
        string? deviceIdentifier = null,
        UserAccountKeysData? accountKeys = null,
        KeyId? userKeyId = null)
    {
        return new FinishSsoJitProvisionTrustedDeviceData
        {
            AccountKeys = accountKeys ?? CreateV2AccountKeys(),
            UserKeyId = userKeyId,
            OrganizationId = organizationId,
            ResetPasswordKey = resetPasswordKey,
            DeviceKeys = deviceIdentifier == null
                ? null
                : new TrustedDeviceKeysData
                {
                    DeviceIdentifier = deviceIdentifier,
                    EncryptedUserKey = "4.device-user-key",
                    EncryptedPublicKey = "2.device|public|key",
                    EncryptedPrivateKey = "2.device|private|key"
                }
        };
    }

    private static UserAccountKeysData CreateV2AccountKeys()
    {
        return new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData =
                new PublicKeyEncryptionKeyPairData("7.wrapped-private-key", "public-key", "signed-public-key"),
            SignatureKeyPairData = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "7.wrapped-signing-key",
                "verifying-key"),
            SecurityStateData = new SecurityStateData { SecurityState = "v2", SecurityVersion = 2 }
        };
    }

    private static async Task AssertNoWritesAsync(SutProvider<FinishSsoJitProvisionTrustedDeviceCommand> sutProvider)
    {
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .SetV2AccountCryptographicStateAsync(default, default!, default);
        await sutProvider.GetDependency<IUserService>().DidNotReceiveWithAnyArgs()
            .SaveUserAsync(default!);
        await sutProvider.GetDependency<IDeviceService>().DidNotReceiveWithAnyArgs()
            .SaveAsync(default(Device)!);
        await sutProvider.GetDependency<IUpdateUserResetPasswordEnrollmentCommand>().DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(default, default, default, default);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgIdAsync(default, default!, default!);
    }
}
