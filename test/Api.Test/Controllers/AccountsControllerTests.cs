using System.Security.Claims;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Controllers;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

public class AccountsControllerTests : IDisposable
{

    private readonly AccountsController _sut;
    private readonly GlobalSettings _globalSettings;
    private readonly ICipherRepository _cipherRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ISendRepository _sendRepository;
    private readonly ISendService _sendService;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly ICaptchaValidationService _captchaValidationService;

    public AccountsControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _userRepository = Substitute.For<IUserRepository>();
        _cipherRepository = Substitute.For<ICipherRepository>();
        _folderRepository = Substitute.For<IFolderRepository>();
        _organizationService = Substitute.For<IOrganizationService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _providerUserRepository = Substitute.For<IProviderUserRepository>();
        _paymentService = Substitute.For<IPaymentService>();
        _globalSettings = new GlobalSettings();
        _sendRepository = Substitute.For<ISendRepository>();
        _sendService = Substitute.For<ISendService>();
        _captchaValidationService = Substitute.For<ICaptchaValidationService>();
        _sut = new AccountsController(
            _globalSettings,
            _cipherRepository,
            _folderRepository,
            _organizationService,
            _organizationUserRepository,
            _providerUserRepository,
            _paymentService,
            _userRepository,
            _userService,
            _sendRepository,
            _sendService,
            _captchaValidationService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public async Task PostPrelogin_WhenUserExists_ShouldReturnUserKdfInfo()
    {
        var userKdfInfo = new UserKdfInformation
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5000
        };
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult(userKdfInfo));

        var response = await _sut.PostPrelogin(new PreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(userKdfInfo.Kdf, response.Kdf);
        Assert.Equal(userKdfInfo.KdfIterations, response.KdfIterations);
    }

    [Fact]
    public async Task PostPrelogin_WhenUserDoesNotExist_ShouldDefaultToSha256And100000Iterations()
    {
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult((UserKdfInformation)null));

        var response = await _sut.PostPrelogin(new PreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(KdfType.PBKDF2_SHA256, response.Kdf);
        Assert.Equal(100000, response.KdfIterations);
    }

    [Fact]
    public async Task PostRegister_ShouldRegisterUser()
    {
        var passwordHash = "abcdef";
        var token = "123456";
        var userGuid = new Guid();
        _userService.RegisterUserAsync(Arg.Any<User>(), passwordHash, token, userGuid)
                    .Returns(Task.FromResult(IdentityResult.Success));
        var request = new RegisterRequestModel
        {
            Name = "Example User",
            Email = "user@example.com",
            MasterPasswordHash = passwordHash,
            MasterPasswordHint = "example",
            Token = token,
            OrganizationUserId = userGuid
        };

        await _sut.PostRegister(request);

        await _userService.Received(1).RegisterUserAsync(Arg.Any<User>(), passwordHash, token, userGuid);
    }

    [Fact]
    public async Task PostRegister_WhenUserServiceFails_ShouldThrowBadRequestException()
    {
        var passwordHash = "abcdef";
        var token = "123456";
        var userGuid = new Guid();
        _userService.RegisterUserAsync(Arg.Any<User>(), passwordHash, token, userGuid)
                    .Returns(Task.FromResult(IdentityResult.Failed()));
        var request = new RegisterRequestModel
        {
            Name = "Example User",
            Email = "user@example.com",
            MasterPasswordHash = passwordHash,
            MasterPasswordHint = "example",
            Token = token,
            OrganizationUserId = userGuid
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegister(request));
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

