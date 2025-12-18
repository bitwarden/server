#nullable enable
using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Validators;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(AccountsKeyManagementController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class AccountsKeyManagementControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_FeatureFlagOff_Throws(
        SutProvider<AccountsKeyManagementController> sutProvider,
        KeyRegenerationRequestModel data)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Is(FeatureFlagKeys.PrivateKeyRegeneration))
            .Returns(false);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RegenerateKeysAsync(data));

        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs(0)
            .GetManyByUserAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IEmergencyAccessRepository>().ReceivedWithAnyArgs(0)
            .GetManyDetailsByGranteeIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IRegenerateUserAsymmetricKeysCommand>().ReceivedWithAnyArgs(0)
            .RegenerateKeysAsync(Arg.Any<UserAsymmetricKeys>(),
                Arg.Any<ICollection<OrganizationUser>>(),
                Arg.Any<ICollection<EmergencyAccessDetails>>());
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_UserNull_Throws(SutProvider<AccountsKeyManagementController> sutProvider,
        KeyRegenerationRequestModel data)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Is(FeatureFlagKeys.PrivateKeyRegeneration))
            .Returns(true);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.RegenerateKeysAsync(data));

        await sutProvider.GetDependency<IOrganizationUserRepository>().ReceivedWithAnyArgs(0)
            .GetManyByUserAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IEmergencyAccessRepository>().ReceivedWithAnyArgs(0)
            .GetManyDetailsByGranteeIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IRegenerateUserAsymmetricKeysCommand>().ReceivedWithAnyArgs(0)
            .RegenerateKeysAsync(Arg.Any<UserAsymmetricKeys>(),
                Arg.Any<ICollection<OrganizationUser>>(),
                Arg.Any<ICollection<EmergencyAccessDetails>>());
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_Success(SutProvider<AccountsKeyManagementController> sutProvider,
        KeyRegenerationRequestModel data, User user, ICollection<OrganizationUser> orgUsers,
        ICollection<EmergencyAccessDetails> accessDetails)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Is(FeatureFlagKeys.PrivateKeyRegeneration))
            .Returns(true);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(Arg.Is(user.Id)).Returns(orgUsers);
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGranteeIdAsync(Arg.Is(user.Id))
            .Returns(accessDetails);

        await sutProvider.Sut.RegenerateKeysAsync(data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyByUserAsync(Arg.Is(user.Id));
        await sutProvider.GetDependency<IEmergencyAccessRepository>().Received(1)
            .GetManyDetailsByGranteeIdAsync(Arg.Is(user.Id));
        await sutProvider.GetDependency<IRegenerateUserAsymmetricKeysCommand>().Received(1)
            .RegenerateKeysAsync(
                Arg.Is<UserAsymmetricKeys>(u =>
                    u.UserId == user.Id && u.PublicKey == data.UserPublicKey &&
                    u.UserKeyEncryptedPrivateKey == data.UserKeyEncryptedUserPrivateKey),
                Arg.Is(orgUsers),
                Arg.Is(accessDetails));
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeysSuccess(SutProvider<AccountsKeyManagementController> sutProvider,
        RotateUserAccountKeysAndDataRequestModel data, User user)
    {
        data.AccountKeys.SignatureKeyPair = null;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IRotateUserAccountKeysCommand>().RotateUserAccountKeysAsync(Arg.Any<User>(), Arg.Any<RotateUserAccountKeysData>())
            .Returns(IdentityResult.Success);
        await sutProvider.Sut.RotateUserAccountKeysAsync(data);

        await sutProvider.GetDependency<IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>, IEnumerable<EmergencyAccess>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountUnlockData.EmergencyAccessUnlockData));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>, IReadOnlyList<OrganizationUser>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountUnlockData.OrganizationAccountRecoveryUnlockData));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountUnlockData.PasskeyUnlockData));

        await sutProvider.GetDependency<IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountData.Ciphers));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountData.Folders));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountData.Sends));

        await sutProvider.GetDependency<IRotateUserAccountKeysCommand>().Received(1)
            .RotateUserAccountKeysAsync(Arg.Is(user), Arg.Is<RotateUserAccountKeysData>(d =>
                d.OldMasterKeyAuthenticationHash == data.OldMasterKeyAuthenticationHash

                && d.MasterPasswordUnlockData.KdfType == data.AccountUnlockData.MasterPasswordUnlockData.KdfType
                && d.MasterPasswordUnlockData.KdfIterations == data.AccountUnlockData.MasterPasswordUnlockData.KdfIterations
                && d.MasterPasswordUnlockData.KdfMemory == data.AccountUnlockData.MasterPasswordUnlockData.KdfMemory
                && d.MasterPasswordUnlockData.KdfParallelism == data.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism
                && d.MasterPasswordUnlockData.Email == data.AccountUnlockData.MasterPasswordUnlockData.Email

                && d.MasterPasswordUnlockData.MasterKeyAuthenticationHash == data.AccountUnlockData.MasterPasswordUnlockData.MasterKeyAuthenticationHash
                && d.MasterPasswordUnlockData.MasterKeyEncryptedUserKey == data.AccountUnlockData.MasterPasswordUnlockData.MasterKeyEncryptedUserKey

                && d.AccountKeys!.PublicKeyEncryptionKeyPairData.WrappedPrivateKey == data.AccountKeys.PublicKeyEncryptionKeyPair!.WrappedPrivateKey
                && d.AccountKeys!.PublicKeyEncryptionKeyPairData.PublicKey == data.AccountKeys.PublicKeyEncryptionKeyPair!.PublicKey
            ));
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_UserCryptoV2_Success_Async(SutProvider<AccountsKeyManagementController> sutProvider,
    RotateUserAccountKeysAndDataRequestModel data, User user)
    {
        data.AccountKeys.SignatureKeyPair = new SignatureKeyPairRequestModel
        {
            SignatureAlgorithm = "ed25519",
            WrappedSigningKey = "wrappedSigningKey",
            VerifyingKey = "verifyingKey"
        };
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IRotateUserAccountKeysCommand>().RotateUserAccountKeysAsync(Arg.Any<User>(), Arg.Any<RotateUserAccountKeysData>())
            .Returns(IdentityResult.Success);
        await sutProvider.Sut.RotateUserAccountKeysAsync(data);

        await sutProvider.GetDependency<IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>, IEnumerable<EmergencyAccess>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountUnlockData.EmergencyAccessUnlockData));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>, IReadOnlyList<OrganizationUser>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountUnlockData.OrganizationAccountRecoveryUnlockData));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountUnlockData.PasskeyUnlockData));

        await sutProvider.GetDependency<IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountData.Ciphers));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountData.Folders));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.AccountData.Sends));

        await sutProvider.GetDependency<IRotateUserAccountKeysCommand>().Received(1)
            .RotateUserAccountKeysAsync(Arg.Is(user), Arg.Is<RotateUserAccountKeysData>(d =>
                d.OldMasterKeyAuthenticationHash == data.OldMasterKeyAuthenticationHash

                && d.MasterPasswordUnlockData.KdfType == data.AccountUnlockData.MasterPasswordUnlockData.KdfType
                && d.MasterPasswordUnlockData.KdfIterations == data.AccountUnlockData.MasterPasswordUnlockData.KdfIterations
                && d.MasterPasswordUnlockData.KdfMemory == data.AccountUnlockData.MasterPasswordUnlockData.KdfMemory
                && d.MasterPasswordUnlockData.KdfParallelism == data.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism
                && d.MasterPasswordUnlockData.Email == data.AccountUnlockData.MasterPasswordUnlockData.Email

                && d.MasterPasswordUnlockData.MasterKeyAuthenticationHash == data.AccountUnlockData.MasterPasswordUnlockData.MasterKeyAuthenticationHash
                && d.MasterPasswordUnlockData.MasterKeyEncryptedUserKey == data.AccountUnlockData.MasterPasswordUnlockData.MasterKeyEncryptedUserKey

                && d.AccountKeys!.PublicKeyEncryptionKeyPairData.WrappedPrivateKey == data.AccountKeys.PublicKeyEncryptionKeyPair!.WrappedPrivateKey
                && d.AccountKeys!.PublicKeyEncryptionKeyPairData.PublicKey == data.AccountKeys.PublicKeyEncryptionKeyPair!.PublicKey
                && d.AccountKeys!.PublicKeyEncryptionKeyPairData.SignedPublicKey == data.AccountKeys.PublicKeyEncryptionKeyPair!.SignedPublicKey
                && d.AccountKeys!.SignatureKeyPairData!.SignatureAlgorithm == Core.KeyManagement.Enums.SignatureAlgorithm.Ed25519
                && d.AccountKeys!.SignatureKeyPairData.WrappedSigningKey == data.AccountKeys.SignatureKeyPair!.WrappedSigningKey
                && d.AccountKeys!.SignatureKeyPairData.VerifyingKey == data.AccountKeys.SignatureKeyPair!.VerifyingKey
            ));
    }


    [Theory]
    [BitAutoData]
    public async Task RotateUserKeyNoUser_Throws(SutProvider<AccountsKeyManagementController> sutProvider,
        RotateUserAccountKeysAndDataRequestModel data)
    {
        data.AccountKeys.SignatureKeyPair = null;
        User? user = null;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IRotateUserAccountKeysCommand>().RotateUserAccountKeysAsync(Arg.Any<User>(), Arg.Any<RotateUserAccountKeysData>())
            .Returns(IdentityResult.Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.RotateUserAccountKeysAsync(data));
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserKeyWrongData_Throws(SutProvider<AccountsKeyManagementController> sutProvider,
        RotateUserAccountKeysAndDataRequestModel data, User user, IdentityErrorDescriber _identityErrorDescriber)
    {
        data.AccountKeys.SignatureKeyPair = null;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IRotateUserAccountKeysCommand>().RotateUserAccountKeysAsync(Arg.Any<User>(), Arg.Any<RotateUserAccountKeysData>())
            .Returns(IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch()));
        try
        {
            await sutProvider.Sut.RotateUserAccountKeysAsync(data);
            Assert.Fail("Should have thrown");
        }
        catch (BadRequestException ex)
        {
            Assert.NotEmpty(ex.ModelState!.Values);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V1_UserNull_Throws(
        SutProvider<AccountsKeyManagementController> sutProvider,
        SetKeyConnectorKeyRequestModel data)
    {
        data.KeyConnectorKeyWrappedUserKey = null;
        data.AccountKeys = null;

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.PostSetKeyConnectorKeyAsync(data));

        await sutProvider.GetDependency<IUserService>().ReceivedWithAnyArgs(0)
            .SetKeyConnectorKeyAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V1_SetKeyConnectorKeyFails_ThrowsBadRequestWithErrorResponse(
        SutProvider<AccountsKeyManagementController> sutProvider,
        SetKeyConnectorKeyRequestModel data, User expectedUser)
    {
        data.KeyConnectorKeyWrappedUserKey = null;
        data.AccountKeys = null;

        expectedUser.PublicKey = null;
        expectedUser.PrivateKey = null;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);
        sutProvider.GetDependency<IUserService>()
            .SetKeyConnectorKeyAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "set key connector key error" }));

        var badRequestException =
            await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostSetKeyConnectorKeyAsync(data));

        Assert.Equal(1, badRequestException.ModelState!.ErrorCount);
        Assert.Equal("set key connector key error", badRequestException.ModelState.Root.Errors[0].ErrorMessage);
        await sutProvider.GetDependency<IUserService>().Received(1)
            .SetKeyConnectorKeyAsync(Arg.Do<User>(user =>
            {
                Assert.Equal(expectedUser.Id, user.Id);
                Assert.Equal(data.Key, user.Key);
                Assert.Equal(data.Kdf, user.Kdf);
                Assert.Equal(data.KdfIterations, user.KdfIterations);
                Assert.Equal(data.KdfMemory, user.KdfMemory);
                Assert.Equal(data.KdfParallelism, user.KdfParallelism);
                Assert.Equal(data.Keys!.PublicKey, user.PublicKey);
                Assert.Equal(data.Keys!.EncryptedPrivateKey, user.PrivateKey);
            }), Arg.Is(data.Key), Arg.Is(data.OrgIdentifier));
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V1_SetKeyConnectorKeySucceeds_OkResponse(
        SutProvider<AccountsKeyManagementController> sutProvider,
        SetKeyConnectorKeyRequestModel data, User expectedUser)
    {
        data.KeyConnectorKeyWrappedUserKey = null;
        data.AccountKeys = null;

        expectedUser.PublicKey = null;
        expectedUser.PrivateKey = null;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);
        sutProvider.GetDependency<IUserService>()
            .SetKeyConnectorKeyAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        await sutProvider.Sut.PostSetKeyConnectorKeyAsync(data);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .SetKeyConnectorKeyAsync(Arg.Do<User>(user =>
            {
                Assert.Equal(expectedUser.Id, user.Id);
                Assert.Equal(data.Key, user.Key);
                Assert.Equal(data.Kdf, user.Kdf);
                Assert.Equal(data.KdfIterations, user.KdfIterations);
                Assert.Equal(data.KdfMemory, user.KdfMemory);
                Assert.Equal(data.KdfParallelism, user.KdfParallelism);
                Assert.Equal(data.Keys!.PublicKey, user.PublicKey);
                Assert.Equal(data.Keys!.EncryptedPrivateKey, user.PrivateKey);
            }), Arg.Is(data.Key), Arg.Is(data.OrgIdentifier));
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V2_UserNull_Throws(
        SutProvider<AccountsKeyManagementController> sutProvider)
    {
        var request = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = "wrapped-user-key",
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = "public-key",
                UserKeyEncryptedAccountPrivateKey = "encrypted-private-key"
            },
            OrgIdentifier = "test-org"
        };

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.PostSetKeyConnectorKeyAsync(request));

        await sutProvider.GetDependency<ISetKeyConnectorKeyCommand>().DidNotReceive()
            .SetKeyConnectorKeyForUserAsync(Arg.Any<User>(), Arg.Any<KeyConnectorKeysData>());
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V2_Success(
        SutProvider<AccountsKeyManagementController> sutProvider,
        User expectedUser)
    {
        var request = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = "wrapped-user-key",
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = "public-key",
                UserKeyEncryptedAccountPrivateKey = "encrypted-private-key"
            },
            OrgIdentifier = "test-org"
        };

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);

        await sutProvider.Sut.PostSetKeyConnectorKeyAsync(request);

        await sutProvider.GetDependency<ISetKeyConnectorKeyCommand>().Received(1)
            .SetKeyConnectorKeyForUserAsync(Arg.Is(expectedUser),
                Arg.Do<KeyConnectorKeysData>(data =>
                {
                    Assert.Equal(request.KeyConnectorKeyWrappedUserKey, data.KeyConnectorKeyWrappedUserKey);
                    Assert.Equal(request.AccountKeys.AccountPublicKey, data.AccountKeys.AccountPublicKey);
                    Assert.Equal(request.AccountKeys.UserKeyEncryptedAccountPrivateKey,
                        data.AccountKeys.UserKeyEncryptedAccountPrivateKey);
                    Assert.Equal(request.OrgIdentifier, data.OrgIdentifier);
                }));
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V2_CommandThrows_PropagatesException(
        SutProvider<AccountsKeyManagementController> sutProvider,
        User expectedUser)
    {
        var request = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = "wrapped-user-key",
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = "public-key",
                UserKeyEncryptedAccountPrivateKey = "encrypted-private-key"
            },
            OrgIdentifier = "test-org"
        };

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);
        sutProvider.GetDependency<ISetKeyConnectorKeyCommand>()
            .When(x => x.SetKeyConnectorKeyForUserAsync(Arg.Any<User>(), Arg.Any<KeyConnectorKeysData>()))
            .Do(_ => throw new BadRequestException("Command failed"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PostSetKeyConnectorKeyAsync(request));

        Assert.Equal("Command failed", exception.Message);
        await sutProvider.GetDependency<ISetKeyConnectorKeyCommand>().Received(1)
            .SetKeyConnectorKeyForUserAsync(Arg.Is(expectedUser),
                Arg.Do<KeyConnectorKeysData>(data =>
                {
                    Assert.Equal(request.KeyConnectorKeyWrappedUserKey, data.KeyConnectorKeyWrappedUserKey);
                    Assert.Equal(request.AccountKeys.AccountPublicKey, data.AccountKeys.AccountPublicKey);
                    Assert.Equal(request.AccountKeys.UserKeyEncryptedAccountPrivateKey,
                        data.AccountKeys.UserKeyEncryptedAccountPrivateKey);
                    Assert.Equal(request.OrgIdentifier, data.OrgIdentifier);
                }));
    }

    [Theory]
    [BitAutoData]
    public async Task PostConvertToKeyConnectorAsync_UserNull_Throws(
        SutProvider<AccountsKeyManagementController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.PostConvertToKeyConnectorAsync());

        await sutProvider.GetDependency<IUserService>().ReceivedWithAnyArgs(0)
            .ConvertToKeyConnectorAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task PostConvertToKeyConnectorAsync_ConvertToKeyConnectorFails_ThrowsBadRequestWithErrorResponse(
        SutProvider<AccountsKeyManagementController> sutProvider,
        User expectedUser)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);
        sutProvider.GetDependency<IUserService>()
            .ConvertToKeyConnectorAsync(Arg.Any<User>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "convert to key connector error" }));

        var badRequestException =
            await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostConvertToKeyConnectorAsync());

        Assert.Equal(1, badRequestException.ModelState!.ErrorCount);
        Assert.Equal("convert to key connector error", badRequestException.ModelState.Root.Errors[0].ErrorMessage);
        await sutProvider.GetDependency<IUserService>().Received(1)
            .ConvertToKeyConnectorAsync(Arg.Is(expectedUser));
    }

    [Theory]
    [BitAutoData]
    public async Task PostConvertToKeyConnectorAsync_ConvertToKeyConnectorSucceeds_OkResponse(
        SutProvider<AccountsKeyManagementController> sutProvider,
        User expectedUser)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);
        sutProvider.GetDependency<IUserService>()
            .ConvertToKeyConnectorAsync(Arg.Any<User>())
            .Returns(IdentityResult.Success);

        await sutProvider.Sut.PostConvertToKeyConnectorAsync();

        await sutProvider.GetDependency<IUserService>().Received(1)
            .ConvertToKeyConnectorAsync(Arg.Is(expectedUser));
    }

    [Theory]
    [BitAutoData]
    public async Task GetKeyConnectorConfirmationDetailsAsync_NoUser_Throws(
        SutProvider<AccountsKeyManagementController> sutProvider, string orgSsoIdentifier)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .ReturnsNull();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.GetKeyConnectorConfirmationDetailsAsync(orgSsoIdentifier));

        await sutProvider.GetDependency<IKeyConnectorConfirmationDetailsQuery>().ReceivedWithAnyArgs(0)
            .Run(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetKeyConnectorConfirmationDetailsAsync_Success(
        SutProvider<AccountsKeyManagementController> sutProvider, User expectedUser, string orgSsoIdentifier)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(expectedUser);
        sutProvider.GetDependency<IKeyConnectorConfirmationDetailsQuery>().Run(orgSsoIdentifier, expectedUser.Id)
            .Returns(
                new KeyConnectorConfirmationDetails { OrganizationName = "test" }
            );

        var result = await sutProvider.Sut.GetKeyConnectorConfirmationDetailsAsync(orgSsoIdentifier);

        Assert.NotNull(result);
        Assert.Equal("test", result.OrganizationName);
        await sutProvider.GetDependency<IKeyConnectorConfirmationDetailsQuery>().Received(1)
            .Run(orgSsoIdentifier, expectedUser.Id);
    }
}
