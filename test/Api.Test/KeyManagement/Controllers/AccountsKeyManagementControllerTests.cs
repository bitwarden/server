#nullable enable
using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Request.Accounts;
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
using Bit.Core.KeyManagement.Models.Data;
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
        RotateUserAccountKeysModel data, User user)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IRotateUserAccountKeysCommand>().RotateUserAccountKeysAsync(Arg.Any<User>(), Arg.Any<RotateUserAccountKeysData>())
            .Returns(IdentityResult.Success);
        await sutProvider.Sut.RotateUserAccountKeys(data);
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.Ciphers));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.Folders));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.Sends));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>, IEnumerable<EmergencyAccess>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.EmergencyAccessUnlockData));
        await sutProvider.GetDependency<IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>, IReadOnlyList<OrganizationUser>>>().Received(1)
            .ValidateAsync(Arg.Any<User>(), Arg.Is(data.OrganizationAccountRecoveryUnlockData));
    }
}
