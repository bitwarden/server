using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Response;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.Test.Wrappers;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;

namespace Bit.Identity.Test.IdentityServer;

public class BaseRequestValidatorTests
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private UserManager<User> _userManager;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IDeviceValidator _deviceValidator;
    private readonly ITwoFactorAuthenticationValidator _twoFactorAuthenticationValidator;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ILogger<BaseRequestValidatorTests> _logger;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IPolicyService _policyService;
    private readonly IFeatureService _featureService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserDecryptionOptionsBuilder _userDecryptionOptionsBuilder;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly IMailService _mailService;

    private readonly BaseRequestValidatorTestWrapper _sut;

    public BaseRequestValidatorTests()
    {
        _userManager = SubstituteUserManager();
        _userService = Substitute.For<IUserService>();
        _eventService = Substitute.For<IEventService>();
        _deviceValidator = Substitute.For<IDeviceValidator>();
        _twoFactorAuthenticationValidator = Substitute.For<ITwoFactorAuthenticationValidator>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _logger = Substitute.For<ILogger<BaseRequestValidatorTests>>();
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = Substitute.For<GlobalSettings>();
        _userRepository = Substitute.For<IUserRepository>();
        _policyService = Substitute.For<IPolicyService>();
        _featureService = Substitute.For<IFeatureService>();
        _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        _userDecryptionOptionsBuilder = Substitute.For<IUserDecryptionOptionsBuilder>();
        _policyRequirementQuery = Substitute.For<IPolicyRequirementQuery>();
        _authRequestRepository = Substitute.For<IAuthRequestRepository>();
        _mailService = Substitute.For<IMailService>();

        _sut = new BaseRequestValidatorTestWrapper(
            _userManager,
            _userService,
            _eventService,
            _deviceValidator,
            _twoFactorAuthenticationValidator,
            _organizationUserRepository,
            _logger,
            _currentContext,
            _globalSettings,
            _userRepository,
            _policyService,
            _featureService,
            _ssoConfigRepository,
            _userDecryptionOptionsBuilder,
            _policyRequirementQuery,
            _authRequestRepository,
            _mailService);
    }

    /* Logic path
     * ValidateAsync -> UpdateFailedAuthDetailsAsync -> _mailService.SendFailedLoginAttemptsEmailAsync
     *            |-> BuildErrorResultAsync -> _eventService.LogUserEventAsync
     *                       (self hosted) |-> _logger.LogWarning()
     *                                     |-> SetErrorResult
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_ContextNotValid_SelfHosted_ShouldBuildErrorResult_ShouldLogWarning(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _globalSettings.SelfHosted = true;
        _sut.isValid = false;

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        _logger.Received(1).LogWarning(Constants.BypassFiltersEventId, "Failed login attempt. ");
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("Username or password is incorrect. Try again.", errorResponse.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceNotValidated_ShouldLogError(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        _sut.isValid = true;

        // 2 -> will result to false with no extra configuration
        // 3 -> set two factor to be false
        _twoFactorAuthenticationValidator
                .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 4 -> set up device validator to fail
        requestContext.KnownDevice = false;
        tokenRequest.GrantType = "password";
        _deviceValidator.ValidateRequestDeviceAsync(Arg.Any<ValidatedTokenRequest>(), Arg.Any<CustomValidatorRequestContext>())
                         .Returns(Task.FromResult(false));

        // 5 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        await _eventService.Received(1)
            .LogUserEventAsync(context.CustomValidatorRequestContext.User.Id, EventType.User_FailedLogIn);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceValidated_ShouldSucceed(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        _sut.isValid = true;

        // 2 -> will result to false with no extra configuration
        // 3 -> set two factor to be false
        _twoFactorAuthenticationValidator
                .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 4 -> set up device validator to pass
        _deviceValidator.ValidateRequestDeviceAsync(Arg.Any<ValidatedTokenRequest>(), Arg.Any<CustomValidatorRequestContext>())
                         .Returns(Task.FromResult(true));

        // 5 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidatedAuthRequest_ConsumedOnSuccess(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        _sut.isValid = true;

        var authRequest = new AuthRequest
        {
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = "",
            RequestIpAddress = "1.1.1.1",
            AccessCode = "password",
            PublicKey = "test_public_key",
            CreationDate = DateTime.UtcNow.AddMinutes(-5),
            ResponseDate = DateTime.UtcNow.AddMinutes(-2),
            Approved = true,
            AuthenticationDate = null, // unused
            UserId = requestContext.User.Id,
        };
        requestContext.ValidatedAuthRequest = authRequest;

        // 2 -> will result to false with no extra configuration
        // 3 -> set two factor to be false
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 4 -> set up device validator to pass
        _deviceValidator.ValidateRequestDeviceAsync(Arg.Any<ValidatedTokenRequest>(), Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(true));

        // 5 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);

        // Check that the auth request was consumed
        await _authRequestRepository.Received(1).ReplaceAsync(Arg.Is<AuthRequest>(ar =>
            ar.AuthenticationDate.HasValue));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidatedAuthRequest_NotConsumed_When2faRequired(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        _sut.isValid = true;

        var authRequest = new AuthRequest
        {
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = "",
            RequestIpAddress = "1.1.1.1",
            AccessCode = "password",
            PublicKey = "test_public_key",
            CreationDate = DateTime.UtcNow.AddMinutes(-5),
            ResponseDate = DateTime.UtcNow.AddMinutes(-2),
            Approved = true,
            AuthenticationDate = null, // unused
            UserId = requestContext.User.Id,
        };
        requestContext.ValidatedAuthRequest = authRequest;

        // 2 -> will result to false with no extra configuration
        // 3 -> set two factor to be required
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        // Act
        await _sut.ValidateAsync(context);

        // Assert we errored for 2fa requirement
        Assert.True(context.GrantResult.IsError);

        // Assert that the auth request was NOT consumed
        await _authRequestRepository.DidNotReceive().ReplaceAsync(Arg.Any<AuthRequest>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TwoFactorTokenInvalid_ShouldSendFailedTwoFactorEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;

        // 1 -> initial validation passes
        _sut.isValid = true;

        // 2 -> enable the FailedTwoFactorEmail feature flag
        _featureService.IsEnabled(FeatureFlagKeys.FailedTwoFactorEmail).Returns(true);

        // 3 -> set up 2FA as required
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        // 4 -> provide invalid 2FA token
        tokenRequest.Raw["TwoFactorToken"] = "invalid_token";
        tokenRequest.Raw["TwoFactorProvider"] = "0"; // Email provider

        // 5 -> set up 2FA verification to fail
        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.Email, "invalid_token")
            .Returns(Task.FromResult(false));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        // Verify that the failed 2FA email was sent
        await _mailService.Received(1)
            .SendFailedTwoFactorAttemptEmailAsync(
                user.Email,
                Arg.Any<DateTime>(),
                Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TwoFactorRememberTokenExpired_ShouldNotSendFailedTwoFactorEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;

        // 1 -> initial validation passes
        _sut.isValid = true;

        // 2 -> enable the FailedTwoFactorEmail feature flag  
        _featureService.IsEnabled(FeatureFlagKeys.FailedTwoFactorEmail).Returns(true);

        // 3 -> set up 2FA as required
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        // 4 -> provide invalid remember token (remember token expired)
        tokenRequest.Raw["TwoFactorToken"] = "expired_remember_token";
        tokenRequest.Raw["TwoFactorProvider"] = "5"; // Remember provider (Remember = 5)

        // 5 -> set up remember token verification to fail
        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.Remember, "expired_remember_token")
            .Returns(Task.FromResult(false));

        // 6 -> set up dummy BuildTwoFactorResultAsync
        var twoFactorResultDict = new Dictionary<string, object>
        {
            { "TwoFactorProviders", new[] { "0", "1" } },
            { "TwoFactorProviders2", new Dictionary<string, object>() }
        };
        _twoFactorAuthenticationValidator
            .BuildTwoFactorResultAsync(user, null)
            .Returns(Task.FromResult(twoFactorResultDict));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        // Verify that the failed 2FA email was NOT sent for remember token expiration
        await _mailService.DidNotReceive()
            .SendFailedTwoFactorAttemptEmailAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    // Test grantTypes that require SSO when a user is in an organization that requires it
    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_OrgSsoRequiredTrue_ShouldSetSsoResult(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = grantType;
        _policyService.AnyPoliciesApplicableToUserAsync(
                        Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
                      .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("SSO authentication is required.", errorResponse.Message);
    }

    // Test grantTypes with RequireSsoPolicyRequirement when feature flag is enabled
    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_WithPolicyRequirementsEnabled_OrgSsoRequiredTrue_ShouldSetSsoResult(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = grantType;
        // Configure requirement to require SSO
        var requirement = new RequireSsoPolicyRequirement { SsoRequired = true };
        _policyRequirementQuery.GetAsync<RequireSsoPolicyRequirement>(Arg.Any<Guid>()).Returns(requirement);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        await _policyService.DidNotReceive().AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("SSO authentication is required.", errorResponse.Message);
    }

    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_WithPolicyRequirementsEnabled_OrgSsoRequiredFalse_ShouldSucceed(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = grantType;
        context.ValidatedTokenRequest.ClientId = "web";

        // Configure requirement to not require SSO
        var requirement = new RequireSsoPolicyRequirement { SsoRequired = false };
        _policyRequirementQuery.GetAsync<RequireSsoPolicyRequirement>(Arg.Any<Guid>()).Returns(requirement);

        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        await _sut.ValidateAsync(context);

        Assert.False(context.GrantResult.IsError);
        await _eventService.Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await _userRepository.Received(1).ReplaceAsync(Arg.Any<User>());
    }

    // Test grantTypes where SSO would be required but the user is not in an
    // organization that requires it
    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_OrgSsoRequiredFalse_ShouldSucceed(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = grantType;

        _policyService.AnyPoliciesApplicableToUserAsync(
                        Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
                      .Returns(Task.FromResult(false));
        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        context.ValidatedTokenRequest.ClientId = "web";

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        await _eventService.Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await _userRepository.Received(1).ReplaceAsync(Arg.Any<User>());

        Assert.False(context.GrantResult.IsError);

    }

    // Test the grantTypes where SSO is in progress or not relevant
    [Theory]
    [BitAutoData("authorization_code")]
    [BitAutoData("client_credentials")]
    public async Task ValidateAsync_GrantTypes_SsoRequiredFalse_ShouldSucceed(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = grantType;

        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        context.ValidatedTokenRequest.ClientId = "web";

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        await _policyService.DidNotReceive().AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        await _eventService.Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await _userRepository.Received(1).ReplaceAsync(Arg.Any<User>());

        Assert.False(context.GrantResult.IsError);
    }

    /* Logic Path
     * ValidateAsync -> UserService.IsLegacyUser -> FailAuthForLegacyUserAsync
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_IsLegacyUser_FailAuthForLegacyUserAsync(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = context.CustomValidatorRequestContext.User;
        user.Key = null;

        context.ValidatedTokenRequest.ClientId = "Not Web";
        _sut.isValid = true;
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        var expectedMessage = "Legacy encryption without a userkey is no longer supported. To recover your account, please contact support";
        Assert.Equal(expectedMessage, errorResponse.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CustomResponse_NoMasterPassword_ShouldSetUserDecryptionOptions(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        _userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
        {
            HasMasterPassword = false,
            MasterPasswordUnlock = null
        }));

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        var customResponse = context.GrantResult.CustomResponse;
        Assert.Contains("UserDecryptionOptions", customResponse);
        Assert.IsType<UserDecryptionOptions>(customResponse["UserDecryptionOptions"]);
        var userDecryptionOptions = (UserDecryptionOptions)customResponse["UserDecryptionOptions"];
        Assert.False(userDecryptionOptions.HasMasterPassword);
        Assert.Null(userDecryptionOptions.MasterPasswordUnlock);
    }

    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 654_321, null, null)]
    [BitAutoData(KdfType.Argon2id, 11, 128, 5)]
    public async Task ValidateAsync_CustomResponse_MasterPassword_ShouldSetUserDecryptionOptions(
        KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        _userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
        {
            HasMasterPassword = true,
            MasterPasswordUnlock = new MasterPasswordUnlockResponseModel
            {
                Kdf = new MasterPasswordUnlockKdfResponseModel
                {
                    KdfType = kdfType,
                    Iterations = kdfIterations,
                    Memory = kdfMemory,
                    Parallelism = kdfParallelism
                },
                MasterKeyEncryptedUserKey = _mockEncryptedString,
                Salt = "test@example.com"
            }
        }));

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        var customResponse = context.GrantResult.CustomResponse;
        Assert.Contains("UserDecryptionOptions", customResponse);
        Assert.IsType<UserDecryptionOptions>(customResponse["UserDecryptionOptions"]);
        var userDecryptionOptions = (UserDecryptionOptions)customResponse["UserDecryptionOptions"];
        Assert.True(userDecryptionOptions.HasMasterPassword);
        Assert.NotNull(userDecryptionOptions.MasterPasswordUnlock);
        Assert.Equal(kdfType, userDecryptionOptions.MasterPasswordUnlock.Kdf.KdfType);
        Assert.Equal(kdfIterations, userDecryptionOptions.MasterPasswordUnlock.Kdf.Iterations);
        Assert.Equal(kdfMemory, userDecryptionOptions.MasterPasswordUnlock.Kdf.Memory);
        Assert.Equal(kdfParallelism, userDecryptionOptions.MasterPasswordUnlock.Kdf.Parallelism);
        Assert.Equal(_mockEncryptedString, userDecryptionOptions.MasterPasswordUnlock.MasterKeyEncryptedUserKey);
        Assert.Equal("test@example.com", userDecryptionOptions.MasterPasswordUnlock.Salt);
    }

    private BaseRequestValidationContextFake CreateContext(
        ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        return new BaseRequestValidationContextFake(
            tokenRequest,
            requestContext,
            grantResult
        );
    }

    private UserManager<User> SubstituteUserManager()
    {
        return new UserManager<User>(Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }
}
