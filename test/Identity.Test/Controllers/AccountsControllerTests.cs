using System.ComponentModel.DataAnnotations;
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
using Bit.Core.KeyManagement.Models.Api.Request;
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
using SignatureKeyPairRequestModelCustomizeAttribute = Bit.Test.Common.AutoFixture.SignatureKeyPairRequestModelCustomizeAttribute;

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

    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_WhenGivenOrgInvite_ShouldRegisterUser(
        string email, string masterPasswordHash, string orgInviteToken, Guid organizationUserId, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, AccountKeysRequestModel accountKeys)
    {
        // Arrange
        var legacyModel = new RegisterFinishRequestModel
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

        var kdfModel = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
        };

        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Kdf = kdfModel,
                Salt = email.ToLowerInvariant().Trim(),
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfModel,
                MasterKeyWrappedUserKey = userSymmetricKey,
                Salt = email.ToLowerInvariant().Trim(),
            },
            AccountKeys = accountKeys
        };

        var legacyUser = legacyModel.ToUser(false);
        var legacyData = legacyModel.ToData();

        var newUser = newModel.ToUser(true);
        var newData = newModel.ToData();

        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), legacyData, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(IdentityResult.Success));
        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), newData, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var legacyResult = await _sut.PostRegisterFinish(legacyModel);
        var newResult = await _sut.PostRegisterFinish(newModel);

        // Assert
        Assert.NotNull(legacyResult);
        await _registerUserCommand.Received(1).RegisterUserViaOrganizationInviteToken(Arg.Is<User>(u =>
            u.Email == legacyUser.Email &&
            u.MasterPasswordHint == legacyUser.MasterPasswordHint &&
            u.Kdf == legacyUser.Kdf &&
            u.KdfIterations == legacyUser.KdfIterations &&
            u.KdfMemory == legacyUser.KdfMemory &&
            u.KdfParallelism == legacyUser.KdfParallelism &&
            u.Key == legacyUser.Key
        ), legacyData, orgInviteToken, organizationUserId);

        Assert.NotNull(newResult);
        await _registerUserCommand.Received(1).RegisterUserViaOrganizationInviteToken(Arg.Is<User>(u =>
            u.Email == newUser.Email &&
            u.MasterPasswordHint == newUser.MasterPasswordHint
        ), newData, orgInviteToken, organizationUserId);
    }

    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_OrgInviteDuplicateUser_ThrowsBadRequestException(
        string email, string masterPasswordHash, string orgInviteToken, Guid organizationUserId, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, AccountKeysRequestModel accountKeys)
    {
        // Arrange
        var legacyModel = new RegisterFinishRequestModel
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

        var kdfModel = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_ITERATIONS.Default
        };

        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Kdf = kdfModel,
                Salt = email.ToLowerInvariant().Trim(),
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfModel,
                MasterKeyWrappedUserKey = userSymmetricKey,
                Salt = email.ToLowerInvariant().Trim(),
            },
            AccountKeys = accountKeys,
        };

        var legacyUser = legacyModel.ToUser(false);
        var legacyData = legacyModel.ToData();

        var newUser = newModel.ToUser(true);
        var newData = newModel.ToData();

        // Duplicates throw 2 errors, one for the email and one for the username
        var duplicateUserNameErrorCode = "DuplicateUserName";
        var duplicateUserNameErrorDesc = $"Username '{email}' is already taken.";

        var duplicateUserEmailErrorCode = "DuplicateEmail";
        var duplicateUserEmailErrorDesc = $"Email '{email}' is already taken.";

        var failedIdentityResult = IdentityResult.Failed(
            new IdentityError { Code = duplicateUserNameErrorCode, Description = duplicateUserNameErrorDesc },
            new IdentityError { Code = duplicateUserEmailErrorCode, Description = duplicateUserEmailErrorDesc }
        );

        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Is<User>(u =>
                u.Email == legacyUser.Email &&
                u.MasterPasswordHint == legacyUser.MasterPasswordHint &&
                u.Kdf == legacyUser.Kdf &&
                u.KdfIterations == legacyUser.KdfIterations &&
                u.KdfMemory == legacyUser.KdfMemory &&
                u.KdfParallelism == legacyUser.KdfParallelism &&
                u.Key == legacyUser.Key
            ), legacyData, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(failedIdentityResult));

        _registerUserCommand.RegisterUserViaOrganizationInviteToken(Arg.Is<User>(u =>
                u.Email == newUser.Email &&
                u.MasterPasswordHint == newUser.MasterPasswordHint
            ), newData, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(failedIdentityResult));

        // Act
        var legacyException = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterFinish(legacyModel));
        var newException = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterFinish(newModel));

        // We filter out the duplicate username error
        // so we should only see the duplicate email error
        Assert.Equal(2, legacyException.ModelState.ErrorCount);
        legacyException.ModelState.TryGetValue(string.Empty, out var legacyModelStateEntry);
        Assert.NotNull(legacyModelStateEntry);
        var legacyModelError = legacyModelStateEntry.Errors.First();
        Assert.Equal(duplicateUserEmailErrorDesc, legacyModelError.ErrorMessage);

        // TODO PM-27326 decrease back to 1 once legacy testing is removed
        Assert.Equal(2, newException.ModelState.ErrorCount);
        newException.ModelState.TryGetValue(string.Empty, out var newModelStateEntry);
        Assert.NotNull(newModelStateEntry);
        var newModelError = newModelStateEntry.Errors.First();
        Assert.Equal(duplicateUserEmailErrorDesc, newModelError.ErrorMessage);
    }

    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_WhenGivenEmailVerificationToken_ShouldRegisterUser(
        string email, string masterPasswordHash, string emailVerificationToken, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, AccountKeysRequestModel accountKeys)
    {
        // Arrange
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys
        };

        var kdfModel = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_PARALLELISM.Default,
        };

        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Kdf = kdfModel,
                Salt = email.ToLowerInvariant().Trim(),
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfModel,
                MasterKeyWrappedUserKey = userSymmetricKey,
                Salt = email.ToLowerInvariant().Trim(),
            },
            AccountKeys = accountKeys,
        };

        var legacyUser = legacyModel.ToUser(false);
        var legacyData = legacyModel.ToData();

        var newUser = newModel.ToUser(true);
        var newData = newModel.ToData();

        _registerUserCommand.RegisterUserViaEmailVerificationToken(Arg.Any<User>(), legacyData, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));
        _registerUserCommand.RegisterUserViaEmailVerificationToken(Arg.Any<User>(), newData, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var legacyResult = await _sut.PostRegisterFinish(legacyModel);
        var newResult = await _sut.PostRegisterFinish(newModel);

        // Assert
        Assert.NotNull(legacyResult);
        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(Arg.Is<User>(u =>
            u.Email == legacyUser.Email &&
            u.MasterPasswordHint == legacyUser.MasterPasswordHint &&
            u.Kdf == legacyUser.Kdf &&
            u.KdfIterations == legacyUser.KdfIterations &&
            u.KdfMemory == legacyUser.KdfMemory &&
            u.KdfParallelism == legacyUser.KdfParallelism &&
            u.Key == legacyUser.Key
        ), legacyData, emailVerificationToken);

        Assert.NotNull(newResult);
        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(Arg.Is<User>(u =>
            u.Email == newUser.Email &&
            u.MasterPasswordHint == newUser.MasterPasswordHint
        ), newData, emailVerificationToken);
    }

    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_WhenGivenEmailVerificationTokenDuplicateUser_ThrowsBadRequestException(
        string email, string masterPasswordHash, string emailVerificationToken, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, AccountKeysRequestModel accountKeys)
    {
        // Arrange
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys
        };

        var kdfModel = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
        };

        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Kdf = kdfModel,
                Salt = email.ToLowerInvariant().Trim(),
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfModel,
                MasterKeyWrappedUserKey = userSymmetricKey,
                Salt = email.ToLowerInvariant().Trim(),
            },
            AccountKeys = accountKeys,
        };

        var legacyUser = legacyModel.ToUser(false);
        var legacyData = legacyModel.ToData();

        var newUser = newModel.ToUser(true);
        var newData = newModel.ToData();

        // Duplicates throw 2 errors, one for the email and one for the username
        var duplicateUserNameErrorCode = "DuplicateUserName";
        var duplicateUserNameErrorDesc = $"Username '{email}' is already taken.";

        var duplicateUserEmailErrorCode = "DuplicateEmail";
        var duplicateUserEmailErrorDesc = $"Email '{email}' is already taken.";

        var failedIdentityResult = IdentityResult.Failed(
            new IdentityError { Code = duplicateUserNameErrorCode, Description = duplicateUserNameErrorDesc },
            new IdentityError { Code = duplicateUserEmailErrorCode, Description = duplicateUserEmailErrorDesc }
        );

        _registerUserCommand.RegisterUserViaEmailVerificationToken(Arg.Is<User>(u =>
                u.Email == legacyUser.Email &&
                u.MasterPasswordHint == legacyUser.MasterPasswordHint &&
                u.Kdf == legacyUser.Kdf &&
                u.KdfIterations == legacyUser.KdfIterations &&
                u.KdfMemory == legacyUser.KdfMemory &&
                u.KdfParallelism == legacyUser.KdfParallelism &&
                u.Key == legacyUser.Key
            ), legacyData, emailVerificationToken)
            .Returns(Task.FromResult(failedIdentityResult));

        _registerUserCommand.RegisterUserViaEmailVerificationToken(Arg.Is<User>(u =>
                u.Email == newUser.Email &&
                u.MasterPasswordHint == newUser.MasterPasswordHint
            ), newData, emailVerificationToken)
            .Returns(Task.FromResult(failedIdentityResult));

        // Act
        var legacyException = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterFinish(legacyModel));
        var newException = await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegisterFinish(newModel));

        // We filter out the duplicate username error
        // so we should only see the duplicate email error
        Assert.Equal(2, legacyException.ModelState.ErrorCount);
        legacyException.ModelState.TryGetValue(string.Empty, out var legacyModelStateEntry);
        Assert.NotNull(legacyModelStateEntry);
        var legacyModelError = legacyModelStateEntry.Errors.First();
        Assert.Equal(duplicateUserEmailErrorDesc, legacyModelError.ErrorMessage);

        // TODO PM-27326 decrease back to 1 once legacy testing is removed
        Assert.Equal(2, newException.ModelState.ErrorCount);
        newException.ModelState.TryGetValue(string.Empty, out var newModelStateEntry);
        Assert.NotNull(newModelStateEntry);
        var newModelError = newModelStateEntry.Errors.First();
        Assert.Equal(duplicateUserEmailErrorDesc, newModelError.ErrorMessage);
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

    // PM-28143 - When removing the old properties, update this test to just test the new properties working
    // as expected.
    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_EmailVerification_BothDataForms_ProduceEquivalentOutcomes(
        string email,
        string emailVerificationToken,
        string masterPasswordHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey,
        AccountKeysRequestModel accountKeys)
    {
        // Arrange: new-form model (MasterPasswordAuthenticationData + MasterPasswordUnlockData)

        var kdfData = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
        };

        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdfData,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = email // salt choice is not validated here during registration
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfData,
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            AccountKeys = accountKeys,
        };

        // Arrange: legacy-form model (MasterPasswordHash + legacy KDF + UserSymmetricKey)
        var legacyModel = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
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

        var newUser = newModel.ToUser(true);
        var newData = newModel.ToData();

        var legacyUser = legacyModel.ToUser(false);
        var legacyData = legacyModel.ToData();

        _registerUserCommand
            .RegisterUserViaEmailVerificationToken(Arg.Any<User>(), legacyData, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));

        _registerUserCommand
            .RegisterUserViaEmailVerificationToken(Arg.Any<User>(), newData, emailVerificationToken)
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

        // Assert: hash forwarded identically from both inputs
        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(
            Arg.Is<User>(u =>
                u.Email == newUser.Email &&
                u.Kdf == newUser.Kdf &&
                u.KdfIterations == newUser.KdfIterations &&
                u.KdfMemory == newUser.KdfMemory &&
                u.KdfParallelism == newUser.KdfParallelism &&
                u.Key == newUser.Key),
            newData,
            emailVerificationToken);

        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(
            Arg.Is<User>(u =>
                u.Email == legacyUser.Email &&
                u.Kdf == legacyUser.Kdf &&
                u.KdfIterations == legacyUser.KdfIterations &&
                u.KdfMemory == legacyUser.KdfMemory &&
                u.KdfParallelism == legacyUser.KdfParallelism &&
                u.Key == legacyUser.Key),
            legacyData,
            emailVerificationToken);
    }

    // PM-28143 - When removing the old properties, update this test to just test the new properties working
    // as expected.
    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_OrgInvite_BothDataForms_ProduceEquivalentOutcomes(
        string email,
        string orgInviteToken,
        Guid organizationUserId,
        string masterPasswordHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey,
        AccountKeysRequestModel accountKeys)
    {
        var kdfData = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
        };

        // Arrange: new-form model (MasterPasswordAuthenticationData + MasterPasswordUnlockData)
        var newModel = new RegisterFinishRequestModel
        {
            Email = email,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = organizationUserId,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdfData,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = email
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdfData,
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            AccountKeys = accountKeys
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

        var newUser = newModel.ToUser(true);
        var newData = newModel.ToData();

        var legacyUser = legacyModel.ToUser(false);
        var legacyData = legacyModel.ToData();

        _registerUserCommand
            .RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), newData, orgInviteToken, organizationUserId)
            .Returns(Task.FromResult(IdentityResult.Success));

        _registerUserCommand
            .RegisterUserViaOrganizationInviteToken(Arg.Any<User>(), legacyData, orgInviteToken, organizationUserId)
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

        // Assert: hash forwarded identically from both inputs
        await _registerUserCommand.Received(1).RegisterUserViaOrganizationInviteToken(
            Arg.Is<User>(u =>
                u.Email == newUser.Email &&
                u.Kdf == newUser.Kdf &&
                u.KdfIterations == newUser.KdfIterations &&
                u.KdfMemory == newUser.KdfMemory &&
                u.KdfParallelism == newUser.KdfParallelism &&
                u.Key == newUser.Key),
            newData,
            orgInviteToken,
            organizationUserId);

        await _registerUserCommand.Received(1).RegisterUserViaOrganizationInviteToken(
            Arg.Is<User>(u =>
                u.Email == legacyUser.Email &&
                u.Kdf == legacyUser.Kdf &&
                u.KdfIterations == legacyUser.KdfIterations &&
                u.KdfMemory == legacyUser.KdfMemory &&
                u.KdfParallelism == legacyUser.KdfParallelism &&
                u.Key == legacyUser.Key),
            legacyData,
            orgInviteToken,
            organizationUserId);
    }

    [Theory, BitAutoData, SignatureKeyPairRequestModelCustomize]
    public async Task PostRegisterFinish_NewForm_UsesUnlockDataForKdfAndKey_WhenRootFieldsNull(
        string email,
        string emailVerificationToken,
        string masterPasswordHash,
        string masterKeyWrappedUserKey,
        int iterations,
        AccountKeysRequestModel accountKeys)
    {
        // Arrange: Provide only unlock-data KDF + key; leave root KDF fields null
        var unlockKdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = iterations
        };

        var model = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                // present but not used by ToUser for KDF/Key
                Kdf = unlockKdf,
                MasterPasswordAuthenticationHash = masterPasswordHash,
                Salt = email
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = unlockKdf,
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            // root KDF fields intentionally null
            Kdf = null,
            KdfIterations = null,
            AccountKeys = accountKeys
        };

        var data = model.ToData();

        _registerUserCommand
            .RegisterUserViaEmailVerificationToken(Arg.Any<User>(), data, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var _ = await _sut.PostRegisterFinish(model);

        // Assert: The user passed to command uses unlock-data values
        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(
            Arg.Is<User>(u => u.Email == email),
            data,
            emailVerificationToken);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterFinish_LegacyForm_UsesRootFields_WhenUnlockDataNull(
        string email,
        string emailVerificationToken,
        string masterPasswordHash,
        string legacyKey,
        string publicKey,
        string encryptedPrivateKey)
    {
        // Arrange: Provide only legacy root KDF + key; no unlock-data provided
        var model = new RegisterFinishRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken,
            MasterPasswordHash = masterPasswordHash,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = legacyKey,
            MasterPasswordUnlock = null,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        var data = model.ToData();

        _registerUserCommand
            .RegisterUserViaEmailVerificationToken(Arg.Any<User>(), data, emailVerificationToken)
            .Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var _ = await _sut.PostRegisterFinish(model);

        // Assert: The user passed to command uses root values
        await _registerUserCommand.Received(1).RegisterUserViaEmailVerificationToken(
            Arg.Is<User>(u =>
                u.Email == email &&
                u.Kdf == KdfType.PBKDF2_SHA256 &&
                u.KdfIterations == AuthConstants.PBKDF2_ITERATIONS.Default &&
                u.Key == legacyKey),
            data,
            emailVerificationToken);
    }

    [Theory, BitAutoData]
    public void RegisterFinishRequestModel_Validate_Throws_WhenUnlockAndAuthDataMismatch(
        string email,
        string authHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey)
    {
        // Arrange: authentication and unlock have different KDF and/or salt
        var authKdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = AuthConstants.PBKDF2_ITERATIONS.Default
        };
        var unlockKdf = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
        };

        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = authKdf,
                MasterPasswordAuthenticationHash = authHash,
                Salt = email
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = unlockKdf,
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        // Provide a minimal valid token type to satisfy model-level token validation
        model.EmailVerificationToken = "test-token";

        var ctx = new ValidationContext(model);

        // Act
        var results = model.Validate(ctx).ToList();

        // Assert mismatched auth/unlock is allowed
        Assert.Empty(results);
    }

    [Theory, BitAutoData]
    public void RegisterFinishRequestModel_Validate_Throws_WhenSaltMismatch(
        string email,
        string authHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey)
    {
        var unlockKdf = new KdfRequestModel
        {
            KdfType = KdfType.Argon2id,
            Iterations = AuthConstants.ARGON2_ITERATIONS.Default,
            Memory = AuthConstants.ARGON2_MEMORY.Default,
            Parallelism = AuthConstants.ARGON2_PARALLELISM.Default
        };

        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = unlockKdf,
                MasterPasswordAuthenticationHash = authHash,
                Salt = email
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = unlockKdf,
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                // Intentionally different salt to force mismatch
                Salt = email + ".mismatch"
            },
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        // Provide a minimal valid token type to satisfy model-level token validation
        model.EmailVerificationToken = "test-token";

        var ctx = new ValidationContext(model);

        // Act
        var results = model.Validate(ctx).ToList();

        // Assert mismatched salts between auth/unlock are allowed
        Assert.Empty(results);
    }

    [Theory, BitAutoData]
    public void RegisterFinishRequestModel_Validate_Throws_WhenAuthHashAndRootHashMismatch(
        string email,
        string authHash,
        string differentRootHash,
        string masterKeyWrappedUserKey,
        string publicKey,
        string encryptedPrivateKey)
    {
        // Arrange: same KDF/salt, but authentication hash differs from legacy root hash
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = AuthConstants.PBKDF2_ITERATIONS.Default
        };

        var model = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = authHash,
                Salt = email
            },
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
                Salt = email
            },
            // Intentionally set the legacy field to a different value to trigger the throw
            MasterPasswordHash = differentRootHash,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            }
        };

        // Provide a minimal valid token type to satisfy model-level token validation
        model.EmailVerificationToken = "test-token";

        var ctx = new ValidationContext(model);

        // Act
        var results = model.Validate(ctx).ToList();

        // Assert: validation result exists with expected message and member names
        var mismatchResult = Assert.Single(results.Where(r =>
            r.ErrorMessage ==
            "MasterPasswordAuthenticationHash and root level MasterPasswordHash provided and are not equal. Only provide one."));
        Assert.Contains("MasterPasswordAuthenticationHash", mismatchResult.MemberNames);
        Assert.Contains("MasterPasswordHash", mismatchResult.MemberNames);
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
