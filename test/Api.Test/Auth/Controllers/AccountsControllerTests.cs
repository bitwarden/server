using System.Security.Claims;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TdeOffboardingPassword.Interfaces;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

public class AccountsControllerTests : IDisposable
{

    private readonly AccountsController _sut;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IPolicyService _policyService;
    private readonly ISetInitialMasterPasswordCommand _setInitialMasterPasswordCommand;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly ITdeOffboardingPasswordCommand _tdeOffboardingPasswordCommand;
    private readonly IFeatureService _featureService;
    private readonly IUserAccountKeysQuery _userAccountKeysQuery;
    private readonly ITwoFactorEmailService _twoFactorEmailService;

    public AccountsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _organizationService = Substitute.For<IOrganizationService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _providerUserRepository = Substitute.For<IProviderUserRepository>();
        _policyService = Substitute.For<IPolicyService>();
        _setInitialMasterPasswordCommand = Substitute.For<ISetInitialMasterPasswordCommand>();
        _twoFactorIsEnabledQuery = Substitute.For<ITwoFactorIsEnabledQuery>();
        _tdeOffboardingPasswordCommand = Substitute.For<ITdeOffboardingPasswordCommand>();
        _featureService = Substitute.For<IFeatureService>();
        _userAccountKeysQuery = Substitute.For<IUserAccountKeysQuery>();
        _twoFactorEmailService = Substitute.For<ITwoFactorEmailService>();
        _sut = new AccountsController(
            _organizationService,
            _organizationUserRepository,
            _providerUserRepository,
            _userService,
            _policyService,
            _setInitialMasterPasswordCommand,
            _tdeOffboardingPasswordCommand,
            _twoFactorIsEnabledQuery,
            _featureService,
            _userAccountKeysQuery,
            _twoFactorEmailService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public async Task PostPasswordHint_ShouldNotifyUserService()
    {
        var email = "user@example.com";

        await _sut.PostPasswordHint(new PasswordHintRequestModel { Email = email });

        await _userService.Received(1).SendMasterPasswordHintAsync(email);
    }

    [Fact]
    public async Task PostEmailToken_ShouldInitiateEmailChange()
    {
        // Arrange
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        const string newEmail = "example@user.com";
        _userService.ValidateClaimedUserDomainAsync(user, newEmail).Returns(IdentityResult.Success);

        // Act
        await _sut.PostEmailToken(new EmailTokenRequestModel { NewEmail = newEmail });

        // Assert
        await _userService.Received(1).InitiateEmailChangeAsync(user, newEmail);
    }

    [Fact]
    public async Task PostEmailToken_WhenValidateClaimedUserDomainAsyncFails_ShouldReturnError()
    {
        // Arrange
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);

        const string newEmail = "example@user.com";

        _userService.ValidateClaimedUserDomainAsync(user, newEmail)
            .Returns(IdentityResult.Failed(new IdentityError
            {
                Code = "TestFailure",
                Description = "This is a test."
            }));


        // Act
        // Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostEmailToken(new EmailTokenRequestModel { NewEmail = newEmail })
        );
    }

    [Fact]
    public async Task PostEmailToken_WhenNotAuthorized_ShouldThrowUnauthorizedAccessException()
    {
        ConfigureUserServiceToReturnNullPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.PostEmailToken(new EmailTokenRequestModel())
        );
    }

    [Fact]
    public async Task PostEmailToken_WhenInvalidPasssword_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToRejectPasswordFor(user);

        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostEmailToken(new EmailTokenRequestModel())
        );
    }

    [Fact]
    public async Task PostEmail_ShouldChangeUserEmail()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        _userService.ChangeEmailAsync(user, default, default, default, default, default)
                    .Returns(Task.FromResult(IdentityResult.Success));

        await _sut.PostEmail(new EmailRequestModel());

        await _userService.Received(1).ChangeEmailAsync(user, default, default, default, default, default);
    }

    [Fact]
    public async Task PostEmail_WhenNotAuthorized_ShouldThrownUnauthorizedAccessException()
    {
        ConfigureUserServiceToReturnNullPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.PostEmail(new EmailRequestModel())
        );
    }

    [Fact]
    public async Task PostEmail_WhenEmailCannotBeChanged_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        _userService.ChangeEmailAsync(user, default, default, default, default, default)
                    .Returns(Task.FromResult(IdentityResult.Failed()));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostEmail(new EmailRequestModel())
        );
    }


    [Fact]
    public async Task PostVerifyEmail_ShouldSendEmailVerification()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);

        await _sut.PostVerifyEmail();

        await _userService.Received(1).SendEmailVerificationAsync(user);
    }

    [Fact]
    public async Task PostVerifyEmail_WhenNotAuthorized_ShouldThrownUnauthorizedAccessException()
    {
        ConfigureUserServiceToReturnNullPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.PostVerifyEmail()
        );
    }

    [Fact]
    public async Task PostVerifyEmailToken_ShouldConfirmEmail()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidIdFor(user);
        _userService.ConfirmEmailAsync(user, Arg.Any<string>())
                    .Returns(Task.FromResult(IdentityResult.Success));

        await _sut.PostVerifyEmailToken(new VerifyEmailRequestModel { UserId = "12345678-1234-1234-1234-123456789012" });

        await _userService.Received(1).ConfirmEmailAsync(user, Arg.Any<string>());
    }

    [Fact]
    public async Task PostVerifyEmailToken_WhenUserDoesNotExist_ShouldThrowUnauthorizedAccessException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnNullUserId();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.PostVerifyEmailToken(new VerifyEmailRequestModel { UserId = "12345678-1234-1234-1234-123456789012" })
        );
    }

    [Fact]
    public async Task PostVerifyEmailToken_WhenEmailConfirmationFails_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidIdFor(user);
        _userService.ConfirmEmailAsync(user, Arg.Any<string>())
                    .Returns(Task.FromResult(IdentityResult.Failed()));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostVerifyEmailToken(new VerifyEmailRequestModel { UserId = "12345678-1234-1234-1234-123456789012" })
        );
    }

    [Fact]
    public async Task PostPassword_ShouldChangePassword()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        _userService.ChangePasswordAsync(user, default, default, default, default)
                    .Returns(Task.FromResult(IdentityResult.Success));

        await _sut.PostPassword(new PasswordRequestModel());

        await _userService.Received(1).ChangePasswordAsync(user, default, default, default, default);
    }

    [Fact]
    public async Task PostPassword_WhenNotAuthorized_ShouldThrowUnauthorizedAccessException()
    {
        ConfigureUserServiceToReturnNullPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.PostPassword(new PasswordRequestModel())
        );
    }

    [Fact]
    public async Task PostPassword_WhenPasswordChangeFails_ShouldBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        _userService.ChangePasswordAsync(user, default, default, default, default)
                    .Returns(Task.FromResult(IdentityResult.Failed()));

        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostPassword(new PasswordRequestModel())
        );
    }

    [Fact]
    public async Task GetApiKey_ShouldReturnApiKeyResponse()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        await _sut.ApiKey(new SecretVerificationRequestModel());
    }

    [Fact]
    public async Task GetApiKey_WhenUserDoesNotExist_ShouldThrowUnauthorizedAccessException()
    {
        ConfigureUserServiceToReturnNullPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ApiKey(new SecretVerificationRequestModel())
        );
    }

    [Fact]
    public async Task GetApiKey_WhenPasswordCheckFails_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToRejectPasswordFor(user);
        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.ApiKey(new SecretVerificationRequestModel())
        );
    }

    [Fact]
    public async Task PostRotateApiKey_ShouldRotateApiKey()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        await _sut.RotateApiKey(new SecretVerificationRequestModel());
    }

    [Fact]
    public async Task PostRotateApiKey_WhenUserDoesNotExist_ShouldThrowUnauthorizedAccessException()
    {
        ConfigureUserServiceToReturnNullPrincipal();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ApiKey(new SecretVerificationRequestModel())
        );
    }

    [Fact]
    public async Task PostRotateApiKey_WhenPasswordCheckFails_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToRejectPasswordFor(user);
        await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.ApiKey(new SecretVerificationRequestModel())
        );
    }


    [Theory]
    [BitAutoData(true, "existingPrivateKey", "existingPublicKey", true)] // allow providing existing keys in the request
    [BitAutoData(true, null, null, true)] // allow not setting the public key when the user already has a key
    [BitAutoData(false, "newPrivateKey", "newPublicKey", true)] // allow setting new keys when the user has no keys
    [BitAutoData(false, null, null, true)] // allow not setting the public key when the user has no keys
    // do not allow single key
    [BitAutoData(false, "existingPrivateKey", null, false)]
    [BitAutoData(false, null, "existingPublicKey", false)]
    [BitAutoData(false, "newPrivateKey", null, false)]
    [BitAutoData(false, null, "newPublicKey", false)]
    [BitAutoData(true, "existingPrivateKey", null, false)]
    [BitAutoData(true, null, "existingPublicKey", false)]
    [BitAutoData(true, "newPrivateKey", null, false)]
    [BitAutoData(true, null, "newPublicKey", false)]
    // reject overwriting existing keys
    [BitAutoData(true, "newPrivateKey", "newPublicKey", false)]
    public async Task PostSetPasswordAsync_WhenUserExistsAndSettingPasswordSucceeds_ShouldHandleKeysCorrectlyAndReturn(
        bool hasExistingKeys,
        string requestPrivateKey,
        string requestPublicKey,
        bool shouldSucceed,
        User user,
        SetPasswordRequestModel setPasswordRequestModel)
    {
        // Arrange
        const string existingPublicKey = "existingPublicKey";
        const string existingEncryptedPrivateKey = "existingPrivateKey";

        if (hasExistingKeys)
        {
            user.PublicKey = existingPublicKey;
            user.PrivateKey = existingEncryptedPrivateKey;
        }
        else
        {
            user.PublicKey = null;
            user.PrivateKey = null;
        }

        if (requestPrivateKey == null && requestPublicKey == null)
        {
            setPasswordRequestModel.Keys = null;
        }
        else
        {
            setPasswordRequestModel.Keys = new KeysRequestModel
            {
                EncryptedPrivateKey = requestPrivateKey,
                PublicKey = requestPublicKey
            };
        }

        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        _setInitialMasterPasswordCommand.SetInitialMasterPasswordAsync(
                user,
                setPasswordRequestModel.MasterPasswordHash,
                setPasswordRequestModel.Key,
                setPasswordRequestModel.OrgIdentifier)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        if (shouldSucceed)
        {
            await _sut.PostSetPasswordAsync(setPasswordRequestModel);
            // Assert
            await _setInitialMasterPasswordCommand.Received(1)
                .SetInitialMasterPasswordAsync(
                    Arg.Is<User>(u => u == user),
                    Arg.Is<string>(s => s == setPasswordRequestModel.MasterPasswordHash),
                    Arg.Is<string>(s => s == setPasswordRequestModel.Key),
                    Arg.Is<string>(s => s == setPasswordRequestModel.OrgIdentifier));

            // Additional Assertions for User object modifications
            Assert.Equal(setPasswordRequestModel.MasterPasswordHint, user.MasterPasswordHint);
            Assert.Equal(setPasswordRequestModel.Kdf, user.Kdf);
            Assert.Equal(setPasswordRequestModel.KdfIterations, user.KdfIterations);
            Assert.Equal(setPasswordRequestModel.KdfMemory, user.KdfMemory);
            Assert.Equal(setPasswordRequestModel.KdfParallelism, user.KdfParallelism);
            Assert.Equal(setPasswordRequestModel.Key, user.Key);
        }
        else
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostSetPasswordAsync(setPasswordRequestModel));
        }
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_WhenUserExistsAndHasKeysAndKeysAreUpdated_ShouldThrowAsync(
    User user,
    SetPasswordRequestModel setPasswordRequestModel)
    {
        // Arrange
        const string existingPublicKey = "existingPublicKey";
        const string existingEncryptedPrivateKey = "existingEncryptedPrivateKey";

        const string newPublicKey = "newPublicKey";
        const string newEncryptedPrivateKey = "newEncryptedPrivateKey";

        user.PublicKey = existingPublicKey;
        user.PrivateKey = existingEncryptedPrivateKey;

        setPasswordRequestModel.Keys = new KeysRequestModel()
        {
            PublicKey = newPublicKey,
            EncryptedPrivateKey = newEncryptedPrivateKey
        };

        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        _setInitialMasterPasswordCommand.SetInitialMasterPasswordAsync(
                user,
                setPasswordRequestModel.MasterPasswordHash,
                setPasswordRequestModel.Key,
                setPasswordRequestModel.OrgIdentifier)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostSetPasswordAsync(setPasswordRequestModel));
    }


    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_WhenUserDoesNotExist_ShouldThrowUnauthorizedAccessException(
        SetPasswordRequestModel setPasswordRequestModel)
    {
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult((User)null));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.PostSetPasswordAsync(setPasswordRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_WhenSettingPasswordFails_ShouldThrowBadRequestException(
        User user,
        SetPasswordRequestModel model)
    {
        model.Keys = null;
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        _setInitialMasterPasswordCommand.SetInitialMasterPasswordAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Some Error" })));

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostSetPasswordAsync(model));
    }

    [Fact]
    public async Task Delete_WithUserManagedByAnOrganization_ThrowsBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        _userService.IsClaimedByAnyOrganizationAsync(user.Id).Returns(true);

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Delete(new SecretVerificationRequestModel()));

        Assert.Equal("Cannot delete accounts owned by an organization. Contact your organization administrator for additional details.", result.Message);
    }

    [Fact]
    public async Task Delete_WithUserNotManagedByAnOrganization_ShouldSucceed()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        _userService.IsClaimedByAnyOrganizationAsync(user.Id).Returns(false);
        _userService.DeleteAsync(user).Returns(IdentityResult.Success);

        await _sut.Delete(new SecretVerificationRequestModel());

        await _userService.Received(1).DeleteAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task SetVerifyDevices_WhenUserDoesNotExist_ShouldThrowUnauthorizedAccessException(
        SetVerifyDevicesRequestModel model)
    {
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult((User)null));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.SetUserVerifyDevicesAsync(model));
    }

    [Theory]
    [BitAutoData]
    public async Task SetVerifyDevices_WhenInvalidSecret_ShouldFail(
        User user, SetVerifyDevicesRequestModel model)
    {
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult((user)));
        _userService.VerifySecretAsync(user, Arg.Any<string>()).Returns(Task.FromResult(false));

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.SetUserVerifyDevicesAsync(model));
    }

    [Theory]
    [BitAutoData]
    public async Task SetVerifyDevices_WhenRequestValid_ShouldSucceed(
        User user, SetVerifyDevicesRequestModel model)
    {
        // Arrange
        user.VerifyDevices = false;
        model.VerifyDevices = true;
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult((user)));
        _userService.VerifySecretAsync(user, Arg.Any<string>()).Returns(Task.FromResult(true));

        // Act
        await _sut.SetUserVerifyDevicesAsync(model);

        await _userService.Received(1).SaveUserAsync(user);
        Assert.Equal(model.VerifyDevices, user.VerifyDevices);
    }

    [Theory]
    [BitAutoData]
    public async Task ResendNewDeviceVerificationEmail_WhenUserNotFound_ShouldFail(
    UnauthenticatedSecretVerificationRequestModel model)
    {
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult((User)null));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.ResendNewDeviceOtpAsync(model));
    }

    [Theory, BitAutoData]
    public async Task ResendNewDeviceVerificationEmail_WhenSecretNotValid_ShouldFail(
        User user,
        UnauthenticatedSecretVerificationRequestModel model)
    {
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        _userService.VerifySecretAsync(user, Arg.Any<string>()).Returns(Task.FromResult(false));

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.ResendNewDeviceOtpAsync(model));
    }

    [Theory, BitAutoData]
    public async Task ResendNewDeviceVerificationEmail_WhenTokenValid_SendsEmail(User user,
        UnauthenticatedSecretVerificationRequestModel model)
    {
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        _userService.VerifySecretAsync(user, Arg.Any<string>()).Returns(Task.FromResult(true));

        // Act
        await _sut.ResendNewDeviceOtpAsync(model);

        // Assert
        await _twoFactorEmailService.Received(1).SendNewDeviceVerificationEmailAsync(user);
    }

    // Below are helper functions that currently belong to this
    // test class, but ultimately may need to be split out into
    // something greater in order to share common test steps with
    // other test suites. They are included here for the time being
    // until that day comes.
    private User GenerateExampleUser()
    {
        return new User
        {
            Email = "user@example.com"
        };
    }

    private void ConfigureUserServiceToReturnNullPrincipal()
    {
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
                    .Returns(Task.FromResult((User)null));
    }

    private void ConfigureUserServiceToReturnValidPrincipalFor(User user)
    {
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
                    .Returns(Task.FromResult(user));
    }

    private void ConfigureUserServiceToRejectPasswordFor(User user)
    {
        _userService.CheckPasswordAsync(user, Arg.Any<string>())
                    .Returns(Task.FromResult(false));
    }

    private void ConfigureUserServiceToAcceptPasswordFor(User user)
    {
        _userService.CheckPasswordAsync(user, Arg.Any<string>())
                    .Returns(Task.FromResult(true));
        _userService.VerifySecretAsync(user, Arg.Any<string>())
                    .Returns(Task.FromResult(true));
    }

    private void ConfigureUserServiceToReturnValidIdFor(User user)
    {
        _userService.GetUserByIdAsync(Arg.Any<Guid>())
                    .Returns(Task.FromResult(user));
    }

    private void ConfigureUserServiceToReturnNullUserId()
    {
        _userService.GetUserByIdAsync(Arg.Any<Guid>())
                    .Returns(Task.FromResult((User)null));
    }
}

