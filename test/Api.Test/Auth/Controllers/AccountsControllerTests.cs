using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.KeyManagement.Validators;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.TdeOffboardingPassword.Interfaces;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Services;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

public class AccountsControllerTests : IDisposable
{

    private readonly AccountsController _sut;
    private readonly GlobalSettings _globalSettings;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IUserService _userService;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IPolicyService _policyService;
    private readonly ISetInitialMasterPasswordCommand _setInitialMasterPasswordCommand;
    private readonly IRotateUserKeyCommand _rotateUserKeyCommand;
    private readonly ITdeOffboardingPasswordCommand _tdeOffboardingPasswordCommand;
    private readonly IFeatureService _featureService;
    private readonly ISubscriberService _subscriberService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;

    private readonly IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>> _cipherValidator;
    private readonly IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>> _folderValidator;
    private readonly IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>> _sendValidator;
    private readonly IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>, IEnumerable<EmergencyAccess>>
        _emergencyAccessValidator;
    private readonly IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>,
            IReadOnlyList<OrganizationUser>>
        _resetPasswordValidator;
    private readonly IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>>
        _webauthnKeyRotationValidator;


    public AccountsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _organizationService = Substitute.For<IOrganizationService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _providerUserRepository = Substitute.For<IProviderUserRepository>();
        _paymentService = Substitute.For<IPaymentService>();
        _globalSettings = new GlobalSettings();
        _policyService = Substitute.For<IPolicyService>();
        _setInitialMasterPasswordCommand = Substitute.For<ISetInitialMasterPasswordCommand>();
        _rotateUserKeyCommand = Substitute.For<IRotateUserKeyCommand>();
        _tdeOffboardingPasswordCommand = Substitute.For<ITdeOffboardingPasswordCommand>();
        _featureService = Substitute.For<IFeatureService>();
        _subscriberService = Substitute.For<ISubscriberService>();
        _referenceEventService = Substitute.For<IReferenceEventService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _cipherValidator =
            Substitute.For<IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>>>();
        _folderValidator =
            Substitute.For<IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>>>();
        _sendValidator = Substitute.For<IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>>>();
        _emergencyAccessValidator = Substitute.For<IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>,
            IEnumerable<EmergencyAccess>>>();
        _webauthnKeyRotationValidator = Substitute.For<IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>>>();
        _resetPasswordValidator = Substitute
            .For<IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>,
                IReadOnlyList<OrganizationUser>>>();

        _sut = new AccountsController(
            _globalSettings,
            _organizationService,
            _organizationUserRepository,
            _providerUserRepository,
            _paymentService,
            _userService,
            _policyService,
            _setInitialMasterPasswordCommand,
            _tdeOffboardingPasswordCommand,
            _rotateUserKeyCommand,
            _featureService,
            _subscriberService,
            _referenceEventService,
            _currentContext,
            _cipherValidator,
            _folderValidator,
            _sendValidator,
            _emergencyAccessValidator,
            _resetPasswordValidator,
            _webauthnKeyRotationValidator
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
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        var newEmail = "example@user.com";

        await _sut.PostEmailToken(new EmailTokenRequestModel { NewEmail = newEmail });

        await _userService.Received(1).InitiateEmailChangeAsync(user, newEmail);
    }

    [Fact]
    public async Task PostEmailToken_WithAccountDeprovisioningEnabled_WhenUserIsNotManagedByAnOrganization_ShouldInitiateEmailChange()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);
        _userService.IsManagedByAnyOrganizationAsync(user.Id).Returns(false);
        var newEmail = "example@user.com";

        await _sut.PostEmailToken(new EmailTokenRequestModel { NewEmail = newEmail });

        await _userService.Received(1).InitiateEmailChangeAsync(user, newEmail);
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
    public async Task PostEmailToken_WithAccountDeprovisioningEnabled_WhenUserIsManagedByAnOrganization_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);
        _userService.IsManagedByAnyOrganizationAsync(user.Id).Returns(true);

        var result = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostEmailToken(new EmailTokenRequestModel())
        );

        Assert.Equal("Cannot change emails for accounts owned by an organization. Contact your organization administrator for additional details.", result.Message);
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
    public async Task PostEmail_WithAccountDeprovisioningEnabled_WhenUserIsNotManagedByAnOrganization_ShouldChangeUserEmail()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        _userService.ChangeEmailAsync(user, default, default, default, default, default)
                    .Returns(Task.FromResult(IdentityResult.Success));
        _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);
        _userService.IsManagedByAnyOrganizationAsync(user.Id).Returns(false);

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
    public async Task PostEmail_WithAccountDeprovisioningEnabled_WhenUserIsManagedByAnOrganization_ShouldThrowBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);
        _userService.IsManagedByAnyOrganizationAsync(user.Id).Returns(true);

        var result = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostEmail(new EmailRequestModel())
        );

        Assert.Equal("Cannot change emails for accounts owned by an organization. Contact your organization administrator for additional details.", result.Message);
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
    [BitAutoData(true, false)]  // User has PublicKey and PrivateKey, and Keys in request are NOT null
    [BitAutoData(true, true)]   // User has PublicKey and PrivateKey, and Keys in request are null
    [BitAutoData(false, false)] // User has neither PublicKey nor PrivateKey, and Keys in request are NOT null
    [BitAutoData(false, true)]  // User has neither PublicKey nor PrivateKey, and Keys in request are null
    public async Task PostSetPasswordAsync_WhenUserExistsAndSettingPasswordSucceeds_ShouldHandleKeysCorrectlyAndReturn(
    bool hasExistingKeys,
    bool shouldSetKeysToNull,
    User user,
    SetPasswordRequestModel setPasswordRequestModel)
    {
        // Arrange
        const string existingPublicKey = "existingPublicKey";
        const string existingEncryptedPrivateKey = "existingEncryptedPrivateKey";

        const string newPublicKey = "newPublicKey";
        const string newEncryptedPrivateKey = "newEncryptedPrivateKey";

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

        if (shouldSetKeysToNull)
        {
            setPasswordRequestModel.Keys = null;
        }
        else
        {
            setPasswordRequestModel.Keys = new KeysRequestModel()
            {
                PublicKey = newPublicKey,
                EncryptedPrivateKey = newEncryptedPrivateKey
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

        if (hasExistingKeys)
        {
            // User Keys should not be modified
            Assert.Equal(existingPublicKey, user.PublicKey);
            Assert.Equal(existingEncryptedPrivateKey, user.PrivateKey);
        }
        else if (!shouldSetKeysToNull)
        {
            // User had no keys so they should be set to the request model's keys
            Assert.Equal(setPasswordRequestModel.Keys.PublicKey, user.PublicKey);
            Assert.Equal(setPasswordRequestModel.Keys.EncryptedPrivateKey, user.PrivateKey);
        }
        else
        {
            // User had no keys and the request model's keys were null, so they should be set to null
            Assert.Null(user.PublicKey);
            Assert.Null(user.PrivateKey);
        }
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
        // Arrange
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        _setInitialMasterPasswordCommand.SetInitialMasterPasswordAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Some Error" })));

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostSetPasswordAsync(model));
    }

    [Fact]
    public async Task Delete_WhenAccountDeprovisioningIsEnabled_WithUserManagedByAnOrganization_ThrowsBadRequestException()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);
        _userService.IsManagedByAnyOrganizationAsync(user.Id).Returns(true);

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Delete(new SecretVerificationRequestModel()));

        Assert.Equal("Cannot delete accounts owned by an organization. Contact your organization administrator for additional details.", result.Message);
    }

    [Fact]
    public async Task Delete_WhenAccountDeprovisioningIsEnabled_WithUserNotManagedByAnOrganization_ShouldSucceed()
    {
        var user = GenerateExampleUser();
        ConfigureUserServiceToReturnValidPrincipalFor(user);
        ConfigureUserServiceToAcceptPasswordFor(user);
        _featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);
        _userService.IsManagedByAnyOrganizationAsync(user.Id).Returns(false);
        _userService.DeleteAsync(user).Returns(IdentityResult.Success);

        await _sut.Delete(new SecretVerificationRequestModel());

        await _userService.Received(1).DeleteAsync(user);
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

