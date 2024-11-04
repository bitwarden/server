using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Identity.Controllers;
using Bit.Identity.Models.Request.Accounts;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class AccountsControllerTests : IDisposable
{

    private readonly AccountsController _sut;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<AccountsController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IRegisterUserCommand _registerUserCommand;
    private readonly ICaptchaValidationService _captchaValidationService;
    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;
    private readonly IGetWebAuthnLoginCredentialAssertionOptionsCommand _getWebAuthnLoginCredentialAssertionOptionsCommand;
    private readonly ISendVerificationEmailForRegistrationCommand _sendVerificationEmailForRegistrationCommand;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IFeatureService _featureService;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _registrationEmailVerificationTokenDataFactory;


    public AccountsControllerTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _logger = Substitute.For<ILogger<AccountsController>>();
        _userRepository = Substitute.For<IUserRepository>();
        _registerUserCommand = Substitute.For<IRegisterUserCommand>();
        _captchaValidationService = Substitute.For<ICaptchaValidationService>();
        _assertionOptionsDataProtector = Substitute.For<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>();
        _getWebAuthnLoginCredentialAssertionOptionsCommand = Substitute.For<IGetWebAuthnLoginCredentialAssertionOptionsCommand>();
        _sendVerificationEmailForRegistrationCommand = Substitute.For<ISendVerificationEmailForRegistrationCommand>();
        _referenceEventService = Substitute.For<IReferenceEventService>();
        _featureService = Substitute.For<IFeatureService>();
        _registrationEmailVerificationTokenDataFactory = Substitute.For<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>();

        _sut = new AccountsController(
            _currentContext,
            _logger,
            _userRepository,
            _registerUserCommand,
            _captchaValidationService,
            _assertionOptionsDataProtector,
            _getWebAuthnLoginCredentialAssertionOptionsCommand,
            _sendVerificationEmailForRegistrationCommand,
            _referenceEventService,
            _featureService,
            _registrationEmailVerificationTokenDataFactory
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
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default
        };
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(userKdfInfo);

        var response = await _sut.PostPrelogin(new PreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(userKdfInfo.Kdf, response.Kdf);
        Assert.Equal(userKdfInfo.KdfIterations, response.KdfIterations);
    }

    [Fact]
    public async Task PostPrelogin_WhenUserDoesNotExist_ShouldDefaultToPBKDF()
    {
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult<UserKdfInformation?>(null));

        var response = await _sut.PostPrelogin(new PreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(KdfType.PBKDF2_SHA256, response.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, response.KdfIterations);
    }

    [Fact]
    public async Task PostRegister_ShouldRegisterUser()
    {
        var passwordHash = "abcdef";
        var token = "123456";
        var userGuid = new Guid();
        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), passwordHash, token, userGuid)
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

        await _registerUserCommand.Received(1).RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), passwordHash, token, userGuid);
    }

    [Fact]
    public async Task PostRegister_WhenUserServiceFails_ShouldThrowBadRequestException()
    {
        var passwordHash = "abcdef";
        var token = "123456";
        var userGuid = new Guid();
        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), passwordHash, token, userGuid)
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

    [Theory]
    [BitAutoData]
    public async Task PostRegisterSendEmailVerification_WhenTokenReturnedFromCommand_Returns200WithToken(string email, string name, bool receiveMarketingEmails)
    {
        // Arrange
        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var token = "fakeToken";

        _sendVerificationEmailForRegistrationCommand.Run(email, name, receiveMarketingEmails).Returns(token);

        // Act
        var result = await _sut.PostRegisterSendVerificationEmail(model);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(token, okResult.Value);

        await _referenceEventService.Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(e => e.Type == ReferenceEventType.SignupEmailSubmit));
    }

    [Theory]
    [BitAutoData]
    public async Task PostRegisterSendEmailVerification_WhenNoTokenIsReturnedFromCommand_Returns204NoContent(string email, string name, bool receiveMarketingEmails)
    {
        // Arrange
        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        _sendVerificationEmailForRegistrationCommand.Run(email, name, receiveMarketingEmails).ReturnsNull();

        // Act
        var result = await _sut.PostRegisterSendVerificationEmail(model);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);
        await _referenceEventService.Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(e => e.Type == ReferenceEventType.SignupEmailSubmit));
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_WhenGivenOrgInvite_ShouldRegisterUser(
        string email, string masterPasswordHash, string orgInviteToken, Guid organizationUserId, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys
        };

        var user = model.ToUser();

        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), masterPasswordHash, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.PostRegisterFinish(model);

        // Assert
        Assert.NotNull(result);
        await _registerUserCommand.Received(1).RegisterUserViaOrganizationInviteToken(Arg.Is<User>(u =>
            u.Email == user.Email &&
            u.MasterPasswordHint == user.MasterPasswordHint &&
            u.Kdf == user.Kdf &&
            u.KdfIterations == user.KdfIterations &&
            u.KdfMemory == user.KdfMemory &&
            u.KdfParallelism == user.KdfParallelism &&
            u.Key == user.Key
        ), masterPasswordHash, orgInviteToken, organizationUserId);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_OrgInviteDuplicateUser_ThrowsBadRequestException(
        string email, string masterPasswordHash, string orgInviteToken, Guid organizationUserId, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys
        };

        var user = model.ToUser();

        // Duplicates throw 2 errors, one for the email and one for the username
        var duplicateUserNameErrorCode = "DuplicateUserName";
        var duplicateUserNameErrorDesc = $"Username '{user.Email}' is already taken.";

        var duplicateUserEmailErrorCode = "DuplicateEmail";
        var duplicateUserEmailErrorDesc = $"Email '{user.Email}' is already taken.";

        var failedIdentityResult = IdentityResult.Failed(
            new IdentityError { Code = duplicateUserNameErrorCode, Description = duplicateUserNameErrorDesc },
            new IdentityError { Code = duplicateUserEmailErrorCode, Description = duplicateUserEmailErrorDesc }
        );

        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Is<User>(u =>
                u.Email == user.Email &&
                u.MasterPasswordHint == user.MasterPasswordHint &&
                u.Kdf == user.Kdf &&
                u.KdfIterations == user.KdfIterations &&
                u.KdfMemory == user.KdfMemory &&
                u.KdfParallelism == user.KdfParallelism &&
                u.Key == user.Key
            ), masterPasswordHash, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(failedIdentityResult));

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterFinish(model));

        // We filter out the duplicate username error
        // so we should only see the duplicate email error
        Assert.Equal(1, exception.ModelState.ErrorCount);
        exception.ModelState.TryGetValue(string.Empty, out var modelStateEntry);
        Assert.NotNull(modelStateEntry);
        var modelError = modelStateEntry.Errors.First();
        Assert.Equal(duplicateUserEmailErrorDesc, modelError.ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_WhenGivenEmailVerificationToken_ShouldRegisterUser(
        string email, string masterPasswordHash, string emailVerificationToken, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys
        };

        var user = model.ToUser();

        _registerUserCommand.RegisterUserViaEmailVerificationToken(Arg.Any<User>(), masterPasswordHash, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var result = await _sut.PostRegisterFinish(model);

        // Assert
        Assert.NotNull(result);
        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(Arg.Is<User>(u =>
            u.Email == user.Email &&
            u.MasterPasswordHint == user.MasterPasswordHint &&
            u.Kdf == user.Kdf &&
            u.KdfIterations == user.KdfIterations &&
            u.KdfMemory == user.KdfMemory &&
            u.KdfParallelism == user.KdfParallelism &&
            u.Key == user.Key
        ), masterPasswordHash, emailVerificationToken);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_WhenGivenEmailVerificationTokenDuplicateUser_ThrowsBadRequestException(
        string email, string masterPasswordHash, string emailVerificationToken, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        // Arrange
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys
        };

        var user = model.ToUser();

        // Duplicates throw 2 errors, one for the email and one for the username
        var duplicateUserNameErrorCode = "DuplicateUserName";
        var duplicateUserNameErrorDesc = $"Username '{user.Email}' is already taken.";

        var duplicateUserEmailErrorCode = "DuplicateEmail";
        var duplicateUserEmailErrorDesc = $"Email '{user.Email}' is already taken.";

        var failedIdentityResult = IdentityResult.Failed(
            new IdentityError { Code = duplicateUserNameErrorCode, Description = duplicateUserNameErrorDesc },
            new IdentityError { Code = duplicateUserEmailErrorCode, Description = duplicateUserEmailErrorDesc }
        );

        _registerUserCommand.RegisterUserViaEmailVerificationToken(Arg.Is<User>(u =>
                u.Email == user.Email &&
                u.MasterPasswordHint == user.MasterPasswordHint &&
                u.Kdf == user.Kdf &&
                u.KdfIterations == user.KdfIterations &&
                u.KdfMemory == user.KdfMemory &&
                u.KdfParallelism == user.KdfParallelism &&
                u.Key == user.Key
            ), masterPasswordHash, emailVerificationToken)
            .Returns(Task.FromResult(failedIdentityResult));

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterFinish(model));

        // We filter out the duplicate username error
        // so we should only see the duplicate email error
        Assert.Equal(1, exception.ModelState.ErrorCount);
        exception.ModelState.TryGetValue(string.Empty, out var modelStateEntry);
        Assert.NotNull(modelStateEntry);
        var modelError = modelStateEntry.Errors.First();
        Assert.Equal(duplicateUserEmailErrorDesc, modelError.ErrorMessage);
    }


    [Theory, BitAutoData]
    public async Task PostRegisterVerificationEmailClicked_WhenTokenIsValid_ShouldReturnOk(string email, string emailVerificationToken)
    {
        // Arrange
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email);
        _registrationEmailVerificationTokenDataFactory
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = registrationEmailVerificationTokenable;
                return true;
            });

        _userRepository.GetByEmailAsync(email).ReturnsNull(); // no existing user

        var requestModel = new RegisterVerificationEmailClickedRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken
        };

        // Act
        var result = await _sut.PostRegisterVerificationEmailClicked(requestModel);

        // Assert
        var okResult = Assert.IsType<OkResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        await _referenceEventService.Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(e =>
            e.Type == ReferenceEventType.SignupEmailClicked
            && e.EmailVerificationTokenValid == true
            && e.UserAlreadyExists == false
            ));
    }

    [Theory, BitAutoData]
    public async Task PostRegisterVerificationEmailClicked_WhenTokenIsInvalid_ShouldReturnBadRequest(string email, string emailVerificationToken)
    {
        // Arrange
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable("wrongEmail");
        _registrationEmailVerificationTokenDataFactory
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = registrationEmailVerificationTokenable;
                return true;
            });

        _userRepository.GetByEmailAsync(email).ReturnsNull(); // no existing user

        var requestModel = new RegisterVerificationEmailClickedRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken
        };

        // Act & assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterVerificationEmailClicked(requestModel));

        await _referenceEventService.Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(e =>
            e.Type == ReferenceEventType.SignupEmailClicked
            && e.EmailVerificationTokenValid == false
            && e.UserAlreadyExists == false
        ));
    }


    [Theory, BitAutoData]
    public async Task PostRegisterVerificationEmailClicked_WhenTokenIsValidButExistingUser_ShouldReturnBadRequest(string email, string emailVerificationToken, User existingUser)
    {
        // Arrange
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email);
        _registrationEmailVerificationTokenDataFactory
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = registrationEmailVerificationTokenable;
                return true;
            });

        _userRepository.GetByEmailAsync(email).Returns(existingUser);

        var requestModel = new RegisterVerificationEmailClickedRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken
        };

        // Act & assert
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterVerificationEmailClicked(requestModel));

        await _referenceEventService.Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(e =>
            e.Type == ReferenceEventType.SignupEmailClicked
            && e.EmailVerificationTokenValid == true
            && e.UserAlreadyExists == true
        ));
    }



}
