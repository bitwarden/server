using System.Reflection;
using System.Text;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
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
    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;
    private readonly IGetWebAuthnLoginCredentialAssertionOptionsCommand _getWebAuthnLoginCredentialAssertionOptionsCommand;
    private readonly ISendVerificationEmailForRegistrationCommand _sendVerificationEmailForRegistrationCommand;
    private readonly IFeatureService _featureService;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _registrationEmailVerificationTokenDataFactory;
    private readonly GlobalSettings _globalSettings;


    public AccountsControllerTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _logger = Substitute.For<ILogger<AccountsController>>();
        _userRepository = Substitute.For<IUserRepository>();
        _registerUserCommand = Substitute.For<IRegisterUserCommand>();
        _assertionOptionsDataProtector = Substitute.For<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>();
        _getWebAuthnLoginCredentialAssertionOptionsCommand = Substitute.For<IGetWebAuthnLoginCredentialAssertionOptionsCommand>();
        _sendVerificationEmailForRegistrationCommand = Substitute.For<ISendVerificationEmailForRegistrationCommand>();
        _featureService = Substitute.For<IFeatureService>();
        _registrationEmailVerificationTokenDataFactory = Substitute.For<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>();
        _globalSettings = Substitute.For<GlobalSettings>();

        _sut = new AccountsController(
            _currentContext,
            _logger,
            _userRepository,
            _registerUserCommand,
            _assertionOptionsDataProtector,
            _getWebAuthnLoginCredentialAssertionOptionsCommand,
            _sendVerificationEmailForRegistrationCommand,
            _featureService,
            _registrationEmailVerificationTokenDataFactory,
            _globalSettings
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public async Task PostPasswordPrelogin_WhenUserExists_ShouldReturnUserKdfInfo()
    {
        var userKdfInfo = new UserKdfInformation
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default
        };
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(userKdfInfo);

        var response = await _sut.PostPasswordPrelogin(new PasswordPreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(userKdfInfo.Kdf, response.Kdf);
        Assert.Equal(userKdfInfo.KdfIterations, response.KdfIterations);
    }

    [Fact]
    public async Task PostPrelogin_And_PostPasswordPrelogin_ShouldUseSamePreloginLogic()
    {
        // Arrange: No user exists and no default HMAC key to force default path
        var email = "same-user@example.com";
        SetDefaultKdfHmacKey(null);
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult<UserKdfInformation?>(null));

        // Act
        var legacyResponse = await _sut.PostPrelogin(new PasswordPreloginRequestModel { Email = email });
        var newResponse = await _sut.PostPasswordPrelogin(new PasswordPreloginRequestModel { Email = email });

        // Assert: Both endpoints yield identical results, implying shared logic path
        Assert.Equal(legacyResponse.Kdf, newResponse.Kdf);
        Assert.Equal(legacyResponse.KdfIterations, newResponse.KdfIterations);
        Assert.Equal(legacyResponse.KdfMemory, newResponse.KdfMemory);
        Assert.Equal(legacyResponse.KdfParallelism, newResponse.KdfParallelism);
        Assert.Equal(legacyResponse.Salt, newResponse.Salt);
        Assert.NotNull(legacyResponse.KdfSettings);
        Assert.NotNull(newResponse.KdfSettings);
        Assert.Equal(legacyResponse.KdfSettings!.KdfType, newResponse.KdfSettings!.KdfType);
        Assert.Equal(legacyResponse.KdfSettings!.Iterations, newResponse.KdfSettings!.Iterations);
        Assert.Equal(legacyResponse.KdfSettings!.Memory, newResponse.KdfSettings!.Memory);
        Assert.Equal(legacyResponse.KdfSettings!.Parallelism, newResponse.KdfSettings!.Parallelism);

        // Both methods should consult the repository once each with the same email
        await _userRepository.Received(2).GetKdfInformationByEmailAsync(Arg.Is<string>(e => e == email));
    }

    [Fact]
    public async Task PostPasswordPrelogin_WhenUserExists_ReturnsNewFieldsAlignedWithLegacy_Argon2()
    {
        var email = "user@example.com";
        var userKdfInfo = new UserKdfInformation
        {
            Kdf = KdfType.Argon2id,
            KdfIterations = AuthConstants.ARGON2_ITERATIONS.Default,
            KdfMemory = AuthConstants.ARGON2_MEMORY.Default,
            KdfParallelism = AuthConstants.ARGON2_PARALLELISM.Default
        };
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(userKdfInfo);

        var response = await _sut.PostPasswordPrelogin(new PasswordPreloginRequestModel { Email = email });

        // New fields exist and match repository values
        Assert.NotNull(response.KdfSettings);
        Assert.Equal(userKdfInfo.Kdf, response.KdfSettings!.KdfType);
        Assert.Equal(userKdfInfo.KdfIterations, response.KdfSettings!.Iterations);
        Assert.Equal(userKdfInfo.KdfMemory, response.KdfSettings!.Memory);
        Assert.Equal(userKdfInfo.KdfParallelism, response.KdfSettings!.Parallelism);

        // New and legacy fields are aligned during migration
        Assert.Equal(response.Kdf, response.KdfSettings!.KdfType);
        Assert.Equal(response.KdfIterations, response.KdfSettings!.Iterations);
        Assert.Equal(response.KdfMemory, response.KdfSettings!.Memory);
        Assert.Equal(response.KdfParallelism, response.KdfSettings!.Parallelism);

        // Salt is set to the input email during migration
        Assert.Equal(email, response.Salt);
    }

    [Fact]
    public async Task PostPasswordPrelogin_WhenUserDoesNotExistAndNoDefaultKdfHmacKeySet_ShouldDefaultToPBKDF()
    {
        SetDefaultKdfHmacKey(null);
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult<UserKdfInformation?>(null));

        var response = await _sut.PostPasswordPrelogin(new PasswordPreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(KdfType.PBKDF2_SHA256, response.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, response.KdfIterations);
    }

    [Fact]
    public async Task PostPasswordPrelogin_NoUser_NoDefaultHmacKey_ReturnsAlignedNewFieldsAndSalt()
    {
        var email = "user@example.com";
        SetDefaultKdfHmacKey(null);
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult<UserKdfInformation?>(null));

        var response = await _sut.PostPasswordPrelogin(new PasswordPreloginRequestModel { Email = email });

        // New fields exist
        Assert.NotNull(response.KdfSettings);

        // New and legacy fields are aligned during migration
        Assert.Equal(response.Kdf, response.KdfSettings!.KdfType);
        Assert.Equal(response.KdfIterations, response.KdfSettings!.Iterations);
        Assert.Equal(response.KdfMemory, response.KdfSettings!.Memory);
        Assert.Equal(response.KdfParallelism, response.KdfSettings!.Parallelism);

        // Salt is set to the input email during migration
        Assert.Equal(email, response.Salt);
    }

    [Theory]
    [BitAutoData]
    public async Task PostPasswordPrelogin_WhenUserDoesNotExistAndDefaultKdfHmacKeyIsSet_ShouldComputeHmacAndReturnExpectedKdf(string email)
    {
        // Arrange:
        var defaultKey = "my-secret-key"u8.ToArray();
        SetDefaultKdfHmacKey(defaultKey);

        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult<UserKdfInformation?>(null));

        var fieldInfo = typeof(AccountsController).GetField("_defaultKdfResults", BindingFlags.NonPublic | BindingFlags.Static);
        if (fieldInfo == null)
            throw new InvalidOperationException("Field '_defaultKdfResults' not found.");

        var defaultKdfResults = (List<UserKdfInformation>)fieldInfo.GetValue(null)!;

        var expectedIndex = GetExpectedKdfIndex(email, defaultKey, defaultKdfResults);
        var expectedKdf = defaultKdfResults[expectedIndex];

        // Act
        var response = await _sut.PostPasswordPrelogin(new PasswordPreloginRequestModel { Email = email });

        // Assert: Ensure the returned KDF matches the expected one from the computed hash
        Assert.Equal(expectedKdf.Kdf, response.Kdf);
        Assert.Equal(expectedKdf.KdfIterations, response.KdfIterations);
        if (expectedKdf.Kdf == KdfType.Argon2id)
        {
            Assert.Equal(expectedKdf.KdfMemory, response.KdfMemory);
            Assert.Equal(expectedKdf.KdfParallelism, response.KdfParallelism);
        }

        // New and legacy fields are aligned during migration
        Assert.NotNull(response.KdfSettings);
        Assert.Equal(response.Kdf, response.KdfSettings!.KdfType);
        Assert.Equal(response.KdfIterations, response.KdfSettings!.Iterations);
        Assert.Equal(response.KdfMemory, response.KdfSettings!.Memory);
        Assert.Equal(response.KdfParallelism, response.KdfSettings!.Parallelism);

        // Salt is set to the input email during migration
        Assert.Equal(email, response.Salt);
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

        _sendVerificationEmailForRegistrationCommand.Run(email, name, receiveMarketingEmails, null).Returns(token);

        // Act
        var result = await _sut.PostRegisterSendVerificationEmail(model);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(token, okResult.Value);
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

        _sendVerificationEmailForRegistrationCommand.Run(email, name, receiveMarketingEmails, null).ReturnsNull();

        // Act
        var result = await _sut.PostRegisterSendVerificationEmail(model);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task PostRegisterSendEmailVerification_WhenFeatureFlagEnabled_PassesFromMarketingToCommandAsync(
        string email, string name, bool receiveMarketingEmails)
    {
        // Arrange
        var fromMarketing = MarketingInitiativeConstants.Premium;
        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            FromMarketing = fromMarketing,
        };

        _featureService.IsEnabled(FeatureFlagKeys.MarketingInitiatedPremiumFlow).Returns(true);

        // Act
        await _sut.PostRegisterSendVerificationEmail(model);

        // Assert
        await _sendVerificationEmailForRegistrationCommand.Received(1)
            .Run(email, name, receiveMarketingEmails, fromMarketing);
    }

    [Theory]
    [BitAutoData]
    public async Task PostRegisterSendEmailVerification_WhenFeatureFlagDisabled_PassesNullFromMarketingToCommandAsync(
        string email, string name, bool receiveMarketingEmails)
    {
        // Arrange
        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            FromMarketing = MarketingInitiativeConstants.Premium, // model includes FromMarketing: "premium"
        };

        _featureService.IsEnabled(FeatureFlagKeys.MarketingInitiatedPremiumFlow).Returns(false);

        // Act
        await _sut.PostRegisterSendVerificationEmail(model);

        // Assert
        await _sendVerificationEmailForRegistrationCommand.Received(1)
            .Run(email, name, receiveMarketingEmails, null); // fromMarketing gets ignored and null gets passed
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
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_EmailVerification_BothDataForms_ProduceEquivalentOutcomes(
        string email,
        string emailVerificationToken,
        string masterPasswordHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey)
    {
        // Arrange: new-form model (MasterPasswordAuthenticationData + MasterPasswordUnlockData)
        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordAuthenticationData = new MasterPasswordAuthenticationData
            {
                Kdf = new KdfSettings
                {
                    KdfType = KdfType.Argon2id,
                    Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
                    Memory = AuthConstants.ARGON2_MEMORY.Default,
                    Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
                },
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = email // salt choice is not validated here during registration
            },
            MasterPasswordUnlockData = new MasterPasswordUnlockData
            {
                Kdf = new KdfSettings
                {
                    KdfType = KdfType.Argon2id,
                    Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
                    Memory = AuthConstants.ARGON2_MEMORY.Default,
                    Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
                },
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        // Arrange: legacy-form model (MasterPasswordHash + legacy KDF + UserSymmetricKey)
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordHash = masterPasswordHash,
            Kdf = KdfType.Argon2id,
            KdfIterations = AuthConstants.ARGON2_ITERATIONS.Default,
            KdfMemory = AuthConstants.ARGON2_MEMORY.Default,
            KdfParallelism = AuthConstants.ARGON2_PARALLELISM.Default,
            UserSymmetricKey = masterKeyWrappedUserKey,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        var newUser = newModel.ToUser();
        var legacyUser = legacyModel.ToUser();

        _registerUserCommand
            .RegisterUserViaEmailVerificationToken(Arg.Any<User>(), masterPasswordHash, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act: call with new form
        var newResult = await _sut.PostRegisterFinish(newModel);
        // Act: call with legacy form
        var legacyResult = await _sut.PostRegisterFinish(legacyModel);

        // Assert: outcomes are identical in effect (success response)
        Assert.NotNull(newResult);
        Assert.NotNull(legacyResult);

        // Assert: effective users are equivalent
        Assert.Equal(legacyUser.Email, newUser.Email);
        Assert.Equal(legacyUser.MasterPasswordHint, newUser.MasterPasswordHint);
        Assert.Equal(legacyUser.Kdf, newUser.Kdf);
        Assert.Equal(legacyUser.KdfIterations, newUser.KdfIterations);
        Assert.Equal(legacyUser.KdfMemory, newUser.KdfMemory);
        Assert.Equal(legacyUser.KdfParallelism, newUser.KdfParallelism);
        Assert.Equal(legacyUser.Key, newUser.Key);
        Assert.Equal(legacyUser.PublicKey, newUser.PublicKey);
        Assert.Equal(legacyUser.PrivateKey, newUser.PrivateKey);

        // Assert: hash forwarded identically from both inputs
        await _registerUserCommand.Received(2).RegisterUserViaEmailVerificationToken(
            Arg.Is<User>(u =>
                u.Email == newUser.Email &&
                u.Kdf == newUser.Kdf &&
                u.KdfIterations == newUser.KdfIterations &&
                u.KdfMemory == newUser.KdfMemory &&
                u.KdfParallelism == newUser.KdfParallelism &&
                u.Key == newUser.Key),
            masterPasswordHash,
            emailVerificationToken);

        await _registerUserCommand.Received(2).RegisterUserViaEmailVerificationToken(
            Arg.Is<User>(u =>
                u.Email == legacyUser.Email &&
                u.Kdf == legacyUser.Kdf &&
                u.KdfIterations == legacyUser.KdfIterations &&
                u.KdfMemory == legacyUser.KdfMemory &&
                u.KdfParallelism == legacyUser.KdfParallelism &&
                u.Key == legacyUser.Key),
            masterPasswordHash,
            emailVerificationToken);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_OrgInvite_BothDataForms_ProduceEquivalentOutcomes(
        string email,
        string orgInviteToken,
        Guid organizationUserId,
        string masterPasswordHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey)
    {
        // Arrange: new-form model (MasterPasswordAuthenticationData + MasterPasswordUnlockData)
        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            MasterPasswordAuthenticationData = new MasterPasswordAuthenticationData
            {
                Kdf = new KdfSettings
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = AuthConstants.PBKDF2_ITERATIONS.Default
                },
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = email
            },
            MasterPasswordUnlockData = new MasterPasswordUnlockData
            {
                Kdf = new KdfSettings
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = AuthConstants.PBKDF2_ITERATIONS.Default
                },
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        // Arrange: legacy-form model (MasterPasswordHash + legacy KDF + UserSymmetricKey)
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            MasterPasswordHash = masterPasswordHash,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = masterKeyWrappedUserKey,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        var newUser = newModel.ToUser();
        var legacyUser = legacyModel.ToUser();

        _registerUserCommand
            .RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), masterPasswordHash, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var newResult = await _sut.PostRegisterFinish(newModel);
        var legacyResult = await _sut.PostRegisterFinish(legacyModel);

        // Assert success
        Assert.NotNull(newResult);
        Assert.NotNull(legacyResult);

        // Assert: effective users are equivalent
        Assert.Equal(legacyUser.Email, newUser.Email);
        Assert.Equal(legacyUser.MasterPasswordHint, newUser.MasterPasswordHint);
        Assert.Equal(legacyUser.Kdf, newUser.Kdf);
        Assert.Equal(legacyUser.KdfIterations, newUser.KdfIterations);
        Assert.Equal(legacyUser.KdfMemory, newUser.KdfMemory);
        Assert.Equal(legacyUser.KdfParallelism, newUser.KdfParallelism);
        Assert.Equal(legacyUser.Key, newUser.Key);
        Assert.Equal(legacyUser.PublicKey, newUser.PublicKey);
        Assert.Equal(legacyUser.PrivateKey, newUser.PrivateKey);

        // Assert: hash forwarded identically from both inputs
        await _registerUserCommand.Received(2).RegisterUserViaOrganizationInviteToken(
            Arg.Is<User>(u =>
                u.Email == newUser.Email &&
                u.Kdf == newUser.Kdf &&
                u.KdfIterations == newUser.KdfIterations &&
                u.KdfMemory == newUser.KdfMemory &&
                u.KdfParallelism == newUser.KdfParallelism &&
                u.Key == newUser.Key),
            masterPasswordHash,
            orgInviteToken,
            organizationUserId);

        await _registerUserCommand.Received(2).RegisterUserViaOrganizationInviteToken(
            Arg.Is<User>(u =>
                u.Email == legacyUser.Email &&
                u.Kdf == legacyUser.Kdf &&
                u.KdfIterations == legacyUser.KdfIterations &&
                u.KdfMemory == legacyUser.KdfMemory &&
                u.KdfParallelism == legacyUser.KdfParallelism &&
                u.Key == legacyUser.Key),
            masterPasswordHash,
            orgInviteToken,
            organizationUserId);
    }

    private void SetDefaultKdfHmacKey(byte[]? newKey)
    {
        var fieldInfo = typeof(AccountsController).GetField("_defaultKdfHmacKey", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fieldInfo == null)
        {
            throw new InvalidOperationException("Field '_defaultKdfHmacKey' not found.");
        }

        fieldInfo.SetValue(_sut, newKey);
    }

    private int GetExpectedKdfIndex(string email, byte[] defaultKey, List<UserKdfInformation> defaultKdfResults)
    {
        // Compute the HMAC hash of the email
        var hmacMessage = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        using var hmac = new System.Security.Cryptography.HMACSHA256(defaultKey);
        var hmacHash = hmac.ComputeHash(hmacMessage);

        // Convert the hash to a number and calculate the index
        var hashHex = BitConverter.ToString(hmacHash).Replace("-", string.Empty).ToLowerInvariant();
        var hashFirst8Bytes = hashHex.Substring(0, 16);
        var hashNumber = long.Parse(hashFirst8Bytes, System.Globalization.NumberStyles.HexNumber);
        return (int)(Math.Abs(hashNumber) % defaultKdfResults.Count);
    }
}
