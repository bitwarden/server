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
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.Test.Wrappers;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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
    private readonly ISsoRequestValidator _ssoRequestValidator;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly FakeLogger<BaseRequestValidatorTests> _logger;
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
    private readonly IUserAccountKeysQuery _userAccountKeysQuery;
    private readonly IClientVersionValidator _clientVersionValidator;

    private readonly BaseRequestValidatorTestWrapper _sut;

    public BaseRequestValidatorTests()
    {
        _userManager = SubstituteUserManager();
        _userService = Substitute.For<IUserService>();
        _eventService = Substitute.For<IEventService>();
        _deviceValidator = Substitute.For<IDeviceValidator>();
        _twoFactorAuthenticationValidator = Substitute.For<ITwoFactorAuthenticationValidator>();
        _ssoRequestValidator = Substitute.For<ISsoRequestValidator>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _logger = new FakeLogger<BaseRequestValidatorTests>();
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
        _userAccountKeysQuery = Substitute.For<IUserAccountKeysQuery>();
        _clientVersionValidator = Substitute.For<IClientVersionValidator>();

        _sut = new BaseRequestValidatorTestWrapper(
            _userManager,
            _userService,
            _eventService,
            _deviceValidator,
            _twoFactorAuthenticationValidator,
            _ssoRequestValidator,
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
            _mailService,
            _userAccountKeysQuery,
            _clientVersionValidator);

        // Default client version validator behavior: allow to pass unless a test overrides.
        _clientVersionValidator
            .ValidateAsync(Arg.Any<User>(), Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(true));
    }

    private void SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(bool recoveryCodeSupportEnabled)
    {
        _featureService
            .IsEnabled(FeatureFlagKeys.RecoveryCodeSupportForSsoRequiredUsers)
            .Returns(recoveryCodeSupportEnabled);
    }

    /* Logic path
     * ValidateAsync -> UpdateFailedAuthDetailsAsync -> _mailService.SendFailedLoginAttemptsEmailAsync
     *            |-> BuildErrorResultAsync -> _eventService.LogUserEventAsync
     *                       (self hosted) |-> _logger.LogWarning()
     *                                     |-> SetErrorResult
     */
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_ContextNotValid_SelfHosted_ShouldBuildErrorResult_ShouldLogWarning(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _globalSettings.SelfHosted = true;
        _sut.isValid = false;

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        var logs = _logger.Collector.GetSnapshot(true);
        Assert.Contains(logs,
            l => l.Level == LogLevel.Warning && l.Message == "Failed login attempt. Is2FARequest: False IpAddress: ");
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("Username or password is incorrect. Try again.", errorResponse.Message);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_DeviceNotValidated_ShouldLogError(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;
        _deviceValidator
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
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

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_DeviceValidated_ShouldSucceed(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        var context = CreateContext(tokenRequest, requestContext, grantResult);

        // 1 -> to pass
        _sut.isValid = true;

        // 2 -> will result to false with no extra configuration
        // 3 -> set two factor to be false
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 4 -> set up device validator to pass
        _deviceValidator
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // 5 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_ValidatedAuthRequest_ConsumedOnSuccess(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        _deviceValidator
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // 5 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);

        // Check that the auth request was consumed
        await _authRequestRepository.Received(1).ReplaceAsync(Arg.Is<AuthRequest>(ar =>
            ar.AuthenticationDate.HasValue));
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_ValidatedAuthRequest_NotConsumed_When2faRequired(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        requestContext.User.TwoFactorProviders = "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"user@test.dev\"}}}";
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        _twoFactorAuthenticationValidator
            .BuildTwoFactorResultAsync(requestContext.User, null)
            .Returns(Task.FromResult(new Dictionary<string, object>
            {
                { "TwoFactorProviders", new[] { "0", "1" } },
                { "TwoFactorProviders2", new Dictionary<string, object>{{"Email", null}} }
            }));

        // Act
        await _sut.ValidateAsync(context);

        // Assert we errored for 2fa requirement
        Assert.True(context.GrantResult.IsError);

        // Assert that the auth request was NOT consumed
        await _authRequestRepository.DidNotReceive().ReplaceAsync(authRequest);

        // Assert that the error is for 2fa
        Assert.Equal("Two-factor authentication required.", context.GrantResult.ErrorDescription);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_TwoFactorTokenInvalid_ShouldSendFailedTwoFactorEmail(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        tokenRequest.Raw["TwoFactorProvider"] = TwoFactorProviderType.Email.ToString();

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
                TwoFactorProviderType.Email,
                Arg.Any<DateTime>(),
                Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_TwoFactorRememberTokenExpired_ShouldNotSendFailedTwoFactorEmail(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        tokenRequest.Raw["TwoFactorProvider"] = "5"; // Remember provider

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
        Assert.Equal("Two-factor authentication required.", context.GrantResult.ErrorDescription);

        // Verify that the failed 2FA email was NOT sent for remember token expiration
        await _mailService.DidNotReceive()
            .SendFailedTwoFactorAttemptEmailAsync(Arg.Any<string>(), Arg.Any<TwoFactorProviderType>(),
                Arg.Any<DateTime>(), Arg.Any<string>());
    }

    // Test grantTypes that require SSO when a user is in an organization that requires it
    [Theory]
    [BitAutoData("password", true)]
    [BitAutoData("password", false)]
    [BitAutoData("webauthn", true)]
    [BitAutoData("webauthn", false)]
    [BitAutoData("refresh_token", true)]
    [BitAutoData("refresh_token", false)]
    public async Task ValidateAsync_GrantTypes_OrgSsoRequiredTrue_ShouldSetSsoResult(
        string grantType,
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
    [BitAutoData("password", true)]
    [BitAutoData("password", false)]
    [BitAutoData("webauthn", true)]
    [BitAutoData("webauthn", false)]
    [BitAutoData("refresh_token", true)]
    [BitAutoData("refresh_token", false)]
    public async Task ValidateAsync_GrantTypes_WithPolicyRequirementsEnabled_OrgSsoRequiredTrue_ShouldSetSsoResult(
        string grantType,
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
    [BitAutoData("password", true)]
    [BitAutoData("password", false)]
    [BitAutoData("webauthn", true)]
    [BitAutoData("webauthn", false)]
    [BitAutoData("refresh_token", true)]
    [BitAutoData("refresh_token", false)]
    public async Task ValidateAsync_GrantTypes_WithPolicyRequirementsEnabled_OrgSsoRequiredFalse_ShouldSucceed(
        string grantType,
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

        await _sut.ValidateAsync(context);

        Assert.False(context.GrantResult.IsError);
        await _eventService.Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await _userRepository.Received(1).ReplaceAsync(Arg.Any<User>());
    }

    // Test grantTypes where SSO would be required but the user is not in an
    // organization that requires it
    [Theory]
    [BitAutoData("password", true)]
    [BitAutoData("password", false)]
    [BitAutoData("webauthn", true)]
    [BitAutoData("webauthn", false)]
    [BitAutoData("refresh_token", true)]
    [BitAutoData("refresh_token", false)]
    public async Task ValidateAsync_GrantTypes_OrgSsoRequiredFalse_ShouldSucceed(
        string grantType,
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

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
    [BitAutoData("authorization_code", true)]
    [BitAutoData("authorization_code", false)]
    [BitAutoData("client_credentials", true)]
    [BitAutoData("client_credentials", false)]
    public async Task ValidateAsync_GrantTypes_SsoRequiredFalse_ShouldSucceed(
        string grantType,
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = grantType;

        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        context.ValidatedTokenRequest.ClientId = "web";
        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

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
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_IsLegacyUser_FailAuthForLegacyUserAsync(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
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
        var expectedMessage =
            "Legacy encryption without a userkey is no longer supported. To recover your account, please contact support";
        Assert.Equal(expectedMessage, errorResponse.Message);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_CustomResponse_NoMasterPassword_ShouldSetUserDecryptionOptions(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        _userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
        {
            HasMasterPassword = false,
            MasterPasswordUnlock = null
        }));
        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

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
    [BitAutoData(true, KdfType.PBKDF2_SHA256, 654_321, null, null)]
    [BitAutoData(false, KdfType.PBKDF2_SHA256, 654_321, null, null)]
    [BitAutoData(true, KdfType.Argon2id, 11, 128, 5)]
    [BitAutoData(false, KdfType.Argon2id, 11, 128, 5)]
    public async Task ValidateAsync_CustomResponse_MasterPassword_ShouldSetUserDecryptionOptions(
        bool featureFlagValue,
        KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        _userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(_userDecryptionOptionsBuilder);
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

        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

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

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_CustomResponse_ShouldIncludeAccountKeys(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        var mockAccountKeys = new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key",
                "test-signed-public-key"
            ),
            SignatureKeyPairData = new SignatureKeyPairData(
                Core.KeyManagement.Enums.SignatureAlgorithm.Ed25519,
                "test-wrapped-signing-key",
                "test-verifying-key"
            ),
            SecurityStateData = new SecurityStateData { SecurityState = "test-security-state", SecurityVersion = 2 }
        };

        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(mockAccountKeys);

        _userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
        {
            HasMasterPassword = true,
            MasterPasswordUnlock = new MasterPasswordUnlockResponseModel
            {
                Kdf = new MasterPasswordUnlockKdfResponseModel
                {
                    KdfType = KdfType.PBKDF2_SHA256,
                    Iterations = 100000
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

        // Verify AccountKeys are included in response
        Assert.Contains("AccountKeys", customResponse);
        Assert.IsType<PrivateKeysResponseModel>(customResponse["AccountKeys"]);

        var accountKeysResponse = (PrivateKeysResponseModel)customResponse["AccountKeys"];
        Assert.NotNull(accountKeysResponse.PublicKeyEncryptionKeyPair);
        Assert.Equal("test-public-key", accountKeysResponse.PublicKeyEncryptionKeyPair.PublicKey);
        Assert.Equal("test-private-key", accountKeysResponse.PublicKeyEncryptionKeyPair.WrappedPrivateKey);
        Assert.Equal("test-signed-public-key", accountKeysResponse.PublicKeyEncryptionKeyPair.SignedPublicKey);

        Assert.NotNull(accountKeysResponse.SignatureKeyPair);
        Assert.Equal("test-wrapped-signing-key", accountKeysResponse.SignatureKeyPair.WrappedSigningKey);
        Assert.Equal("test-verifying-key", accountKeysResponse.SignatureKeyPair.VerifyingKey);

        Assert.NotNull(accountKeysResponse.SecurityState);
        Assert.Equal("test-security-state", accountKeysResponse.SecurityState.SecurityState);
        Assert.Equal(2, accountKeysResponse.SecurityState.SecurityVersion);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_CustomResponse_AccountKeysQuery_SkippedWhenPrivateKeyIsNull(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        requestContext.User.PrivateKey = null;

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

        // Verify that the account keys query wasn't called.
        await _userAccountKeysQuery.Received(0).Run(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_CustomResponse_AccountKeysQuery_CalledWithCorrectUser(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        var expectedUser = requestContext.User;

        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

        _userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(_userDecryptionOptionsBuilder);
        _userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions()));

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

        // Verify that the account keys query was called with the correct user
        await _userAccountKeysQuery.Received(1).Run(Arg.Is<User>(u => u.Id == expectedUser.Id));
    }

    /// <summary>
    /// Tests the core PM-21153 feature: SSO-required users can use recovery codes to disable 2FA,
    /// but must then authenticate via SSO with a descriptive message about the recovery.
    /// This test validates:
    /// 1. Validation order is changed (2FA before SSO) when recovery code is provided
    /// 2. Recovery code successfully validates and sets TwoFactorRecoveryRequested flag
    /// 3. SSO validation then fails with recovery-specific message
    /// 4. User is NOT logged in (must authenticate via IdP)
    /// </summary>
    [Theory]
    [BitAutoData(true)]  // Feature flag ON - new behavior
    [BitAutoData(false)] // Feature flag OFF - should fail at SSO before 2FA recovery
    public async Task ValidateAsync_RecoveryCodeForSsoRequiredUser_BlocksWithDescriptiveMessage(
        bool featureFlagEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagEnabled);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;

        // Reset state that AutoFixture may have populated
        requestContext.TwoFactorRecoveryRequested = false;
        requestContext.RememberMeRequested = false;

        // 1. Master password is valid
        _sut.isValid = true;

        // 2. SSO is required (this user is in an org that requires SSO)
        _policyService.AnyPoliciesApplicableToUserAsync(
                        Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
                      .Returns(Task.FromResult(true));

        // 3. 2FA is required
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(user, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        // 4. Provide a RECOVERY CODE (this triggers the special validation order)
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code-12345";

        // 5. Recovery code is valid (UserService.RecoverTwoFactorAsync will be called internally)
        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code-12345")
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError, "Authentication should fail - SSO required after recovery");

        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];

        if (featureFlagEnabled)
        {
            // NEW BEHAVIOR: Recovery succeeds, then SSO blocks with descriptive message
            Assert.Equal(
                "Two-factor recovery has been performed. SSO authentication is required.",
                errorResponse.Message);

            // Verify recovery was marked
            Assert.True(requestContext.TwoFactorRecoveryRequested,
                "TwoFactorRecoveryRequested flag should be set");
        }
        else
        {
            // LEGACY BEHAVIOR: SSO blocks BEFORE recovery can happen
            Assert.Equal(
                "SSO authentication is required.",
                errorResponse.Message);

            // Recovery never happened because SSO checked first
            Assert.False(requestContext.TwoFactorRecoveryRequested,
                "TwoFactorRecoveryRequested should be false (SSO blocked first)");
        }

        // In both cases: User is NOT logged in
        await _eventService.DidNotReceive().LogUserEventAsync(user.Id, EventType.User_LoggedIn);
    }

    /// <summary>
    /// Tests that validation order changes when a recovery code is PROVIDED (even if invalid).
    /// This ensures the RecoveryCodeRequestForSsoRequiredUserScenario() logic is based on
    /// request structure, not validation outcome. An SSO-required user who provides an
    /// INVALID recovery code should:
    /// 1. Have 2FA validated BEFORE SSO (new order)
    /// 2. Get a 2FA error (invalid token)
    /// 3. NOT get the recovery-specific SSO message (because recovery didn't complete)
    /// 4. NOT be logged in
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_InvalidRecoveryCodeForSsoRequiredUser_FailsAt2FA(
        bool featureFlagEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagEnabled);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;

        // 1. Master password is valid
        _sut.isValid = true;

        // 2. SSO is required
        _policyService.AnyPoliciesApplicableToUserAsync(
                        Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
                      .Returns(Task.FromResult(true));

        // 3. 2FA is required
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(user, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        // 4. Provide a RECOVERY CODE (triggers validation order change)
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "INVALID-recovery-code";

        // 5. Recovery code is INVALID
        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.RecoveryCode, "INVALID-recovery-code")
            .Returns(Task.FromResult(false));

        // 6. Setup for failed 2FA email (if feature flag enabled)
        _featureService.IsEnabled(FeatureFlagKeys.FailedTwoFactorEmail).Returns(true);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError, "Authentication should fail - invalid recovery code");

        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];

        if (featureFlagEnabled)
        {
            // NEW BEHAVIOR: 2FA is checked first (due to recovery code request), fails with 2FA error
            Assert.Equal(
                "Two-step token is invalid. Try again.",
                errorResponse.Message);

            // Recovery was attempted but failed - flag should NOT be set
            Assert.False(requestContext.TwoFactorRecoveryRequested,
                "TwoFactorRecoveryRequested should be false (recovery failed)");

            // Verify failed 2FA email was sent
            await _mailService.Received(1).SendFailedTwoFactorAttemptEmailAsync(
                user.Email,
                TwoFactorProviderType.RecoveryCode,
                Arg.Any<DateTime>(),
                Arg.Any<string>());

            // Verify failed login event was logged
            await _eventService.Received(1).LogUserEventAsync(user.Id, EventType.User_FailedLogIn2fa);
        }
        else
        {
            // LEGACY BEHAVIOR: SSO is checked first, blocks before 2FA
            Assert.Equal(
                "SSO authentication is required.",
                errorResponse.Message);

            // 2FA validation never happened
            await _mailService.DidNotReceive().SendFailedTwoFactorAttemptEmailAsync(
                Arg.Any<string>(),
                Arg.Any<TwoFactorProviderType>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>());
        }

        // In both cases: User is NOT logged in
        await _eventService.DidNotReceive().LogUserEventAsync(user.Id, EventType.User_LoggedIn);

        // Verify user failed login count was updated (in new behavior path)
        if (featureFlagEnabled)
        {
            await _userRepository.Received(1).ReplaceAsync(Arg.Is<User>(u =>
                u.Id == user.Id && u.FailedLoginCount > 0));
        }
    }

    /// <summary>
    /// Tests that non-SSO users can successfully use recovery codes to disable 2FA and log in.
    /// This validates:
    /// 1. Validation order changes to 2FA-first when recovery code is provided
    /// 2. Recovery code validates successfully
    /// 3. SSO check passes (user not in SSO-required org)
    /// 4. User successfully logs in
    /// 5. TwoFactorRecoveryRequested flag is set (for logging/audit purposes)
    /// This is the "happy path" for recovery code usage.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_RecoveryCodeForNonSsoUser_SuccessfulLogin(
        bool featureFlagEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagEnabled);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;

        // 1. Master password is valid
        _sut.isValid = true;

        // 2. SSO is NOT required (this is a regular user, not in SSO org)
        _policyService.AnyPoliciesApplicableToUserAsync(
                        Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
                      .Returns(Task.FromResult(false));

        // 3. 2FA is required
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(user, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        // 4. Provide a RECOVERY CODE
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code-67890";

        // 5. Recovery code is valid
        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code-67890")
            .Returns(Task.FromResult(true));

        // 6. Device validation passes
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // 7. User is not legacy
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // 8. Setup user account keys for successful login response
        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError, "Authentication should succeed for non-SSO user with valid recovery code");

        // Verify user successfully logged in
        await _eventService.Received(1).LogUserEventAsync(user.Id, EventType.User_LoggedIn);

        // Verify failed login count was reset (successful login)
        await _userRepository.Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id && u.FailedLoginCount == 0));

        if (featureFlagEnabled)
        {
            // NEW BEHAVIOR: Recovery flag should be set for audit purposes
            Assert.True(requestContext.TwoFactorRecoveryRequested,
                "TwoFactorRecoveryRequested flag should be set for audit/logging");
        }
        else
        {
            // LEGACY BEHAVIOR: Recovery flag doesn't exist, but login still succeeds
            // (SSO check happens before 2FA in legacy, but user is not SSO-required so both pass)
            Assert.False(requestContext.TwoFactorRecoveryRequested,
                "TwoFactorRecoveryRequested should be false in legacy mode");
        }
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_ClientVersionValidator_IsInvoked_ForFeatureFlagStates(
        bool featureFlagValue,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(featureFlagValue);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true; // ensure initial context validation passes

        // Force a grant type that will evaluate SSO after client version validation
        context.ValidatedTokenRequest.GrantType = "password";

        // Make client version validation succeed but ensure it's invoked
        _clientVersionValidator
            .ValidateAsync(requestContext.User, requestContext)
            .Returns(Task.FromResult(true));

        // Ensure SSO requirement triggers an early stop after version validation to avoid success path setup
        _policyService.AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        await _clientVersionValidator.Received(1)
            .ValidateAsync(requestContext.User, requestContext);
    }

    /// <summary>
    /// Tests that when RedirectOnSsoRequired is DISABLED, the legacy SSO validation path is used.
    /// This validates the deprecated RequireSsoLoginAsync method is called and SSO requirement
    /// is checked using the old PolicyService.AnyPoliciesApplicableToUserAsync approach.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_RedirectOnSsoRequired_Disabled_UsesLegacySsoValidation(
        bool recoveryCodeFeatureEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(recoveryCodeFeatureEnabled);
        _featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired).Returns(false);

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;

        // SSO is required via legacy path
        _policyService.AnyPoliciesApplicableToUserAsync(
            Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("SSO authentication is required.", errorResponse.Message);

        // Verify legacy path was used
        await _policyService.Received(1).AnyPoliciesApplicableToUserAsync(
            requestContext.User.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);

        // Verify new SsoRequestValidator was NOT called
        await _ssoRequestValidator.DidNotReceive().ValidateAsync(
            Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>(), Arg.Any<CustomValidatorRequestContext>());
    }

    /// <summary>
    /// Tests that when RedirectOnSsoRequired is ENABLED, the new ISsoRequestValidator is used
    /// instead of the legacy RequireSsoLoginAsync method.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_RedirectOnSsoRequired_Enabled_UsesNewSsoRequestValidator(
        bool recoveryCodeFeatureEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(recoveryCodeFeatureEnabled);
        _featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired).Returns(true);

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;

        // Configure SsoRequestValidator to indicate SSO is required
        _ssoRequestValidator.ValidateAsync(
            Arg.Any<User>(),
            Arg.Any<ValidatedTokenRequest>(),
            Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(false)); // false = SSO required

        // Set up the ValidationErrorResult that SsoRequestValidator would set
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = "sso_required",
            ErrorDescription = "SSO authentication is required."
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { "ErrorModel", new ErrorResponseModel("SSO authentication is required.") }
        };

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);

        // Verify new SsoRequestValidator was called
        await _ssoRequestValidator.Received(1).ValidateAsync(
            requestContext.User,
            tokenRequest,
            requestContext);

        // Verify legacy path was NOT used
        await _policyService.DidNotReceive().AnyPoliciesApplicableToUserAsync(
            Arg.Any<Guid>(), Arg.Any<PolicyType>(), Arg.Any<OrganizationUserStatusType>());
    }

    /// <summary>
    /// Tests that when RedirectOnSsoRequired is ENABLED and SSO is NOT required,
    /// authentication continues successfully through the new validation path.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_RedirectOnSsoRequired_Enabled_SsoNotRequired_SuccessfulLogin(
        bool recoveryCodeFeatureEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(recoveryCodeFeatureEnabled);
        _featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired).Returns(true);

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;
        tokenRequest.ClientId = "web";

        // SsoRequestValidator returns true (SSO not required)
        _ssoRequestValidator.ValidateAsync(
            Arg.Any<User>(),
            Arg.Any<ValidatedTokenRequest>(),
            Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(true));

        // No 2FA required
        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // Device validation passes
        _deviceValidator.ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // User is not legacy
        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        _userAccountKeysQuery.Run(Arg.Any<User>()).Returns(new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                "test-private-key",
                "test-public-key"
            )
        });

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        await _eventService.Received(1).LogUserEventAsync(requestContext.User.Id, EventType.User_LoggedIn);

        // Verify new validator was used
        await _ssoRequestValidator.Received(1).ValidateAsync(
            requestContext.User,
            tokenRequest,
            requestContext);
    }

    /// <summary>
    /// Tests that when RedirectOnSsoRequired is ENABLED and SSO validation returns a custom response
    /// (e.g., with organization identifier), that custom response is properly propagated to the result.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ValidateAsync_RedirectOnSsoRequired_Enabled_PropagatesCustomResponse(
        bool recoveryCodeFeatureEnabled,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(recoveryCodeFeatureEnabled);
        _featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired).Returns(true);
        _sut.isValid = true;

        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;

        // SsoRequestValidator sets custom response with organization identifier
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = "sso_required",
            ErrorDescription = "SSO authentication is required."
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { "ErrorModel", new ErrorResponseModel("SSO authentication is required.") },
            { "SsoOrganizationIdentifier", "test-org-identifier" }
        };

        var context = CreateContext(tokenRequest, requestContext, grantResult);

        _ssoRequestValidator.ValidateAsync(
            Arg.Any<User>(),
            Arg.Any<ValidatedTokenRequest>(),
            Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(false));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        Assert.NotNull(context.GrantResult.CustomResponse);
        Assert.Contains("SsoOrganizationIdentifier", context.CustomValidatorRequestContext.CustomResponse);
        Assert.Equal("test-org-identifier", context.CustomValidatorRequestContext.CustomResponse["SsoOrganizationIdentifier"]);
    }

    /// <summary>
    /// Tests that when RedirectOnSsoRequired is DISABLED and a user with 2FA recovery completes recovery,
    /// but SSO is required, the legacy error message is returned (without the recovery-specific message).
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_RedirectOnSsoRequired_Disabled_RecoveryWithSso_LegacyMessage(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(true);
        _featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired).Returns(false);

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        // Recovery code scenario
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code";

        // 2FA with recovery
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(requestContext.User, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code")
            .Returns(Task.FromResult(true));

        // SSO is required (legacy check)
        _policyService.AnyPoliciesApplicableToUserAsync(
            Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(true));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];

        // Legacy behavior: recovery-specific message IS shown even without RedirectOnSsoRequired
        Assert.Equal("Two-factor recovery has been performed. SSO authentication is required.", errorResponse.Message);

        // But legacy validation path was used
        await _policyService.Received(1).AnyPoliciesApplicableToUserAsync(
            requestContext.User.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
    }

    /// <summary>
    /// Tests that when RedirectOnSsoRequired is ENABLED and recovery code is used for SSO-required user,
    /// the SsoRequestValidator provides the recovery-specific error message.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_RedirectOnSsoRequired_Enabled_RecoveryWithSso_NewValidatorMessage(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext] CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        SetupRecoveryCodeSupportForSsoRequiredUsersFeatureFlag(true);
        _featureService.IsEnabled(FeatureFlagKeys.RedirectOnSsoRequired).Returns(true);

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _sut.isValid = true;

        // Recovery code scenario
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code";

        // 2FA with recovery
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));

        _twoFactorAuthenticationValidator
            .VerifyTwoFactorAsync(requestContext.User, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code")
            .Returns(Task.FromResult(true));

        // SsoRequestValidator handles the recovery + SSO scenario
        requestContext.TwoFactorRecoveryRequested = true;
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = "sso_required",
            ErrorDescription = "Two-factor recovery has been performed. SSO authentication is required."
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { "ErrorModel", new ErrorResponseModel("Two-factor recovery has been performed. SSO authentication is required.") }
        };

        _ssoRequestValidator.ValidateAsync(
            Arg.Any<User>(),
            Arg.Any<ValidatedTokenRequest>(),
            Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(false));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.CustomValidatorRequestContext.CustomResponse["ErrorModel"];
        Assert.Equal("Two-factor recovery has been performed. SSO authentication is required.", errorResponse.Message);

        // Verify new validator was used
        await _ssoRequestValidator.Received(1).ValidateAsync(
            requestContext.User,
            tokenRequest,
            Arg.Is<CustomValidatorRequestContext>(ctx => ctx.TwoFactorRecoveryRequested));

        // Verify legacy path was NOT used
        await _policyService.DidNotReceive().AnyPoliciesApplicableToUserAsync(
            Arg.Any<Guid>(), Arg.Any<PolicyType>(), Arg.Any<OrganizationUserStatusType>());
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
