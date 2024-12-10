using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    private UserManager<User> _userManager;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IDeviceValidator _deviceValidator;
    private readonly ITwoFactorAuthenticationValidator _twoFactorAuthenticationValidator;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<BaseRequestValidatorTests> _logger;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IPolicyService _policyService;
    private readonly IFeatureService _featureService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserDecryptionOptionsBuilder _userDecryptionOptionsBuilder;

    private readonly BaseRequestValidatorTestWrapper _sut;

    public BaseRequestValidatorTests()
    {
        _userManager = SubstituteUserManager();
        _userService = Substitute.For<IUserService>();
        _eventService = Substitute.For<IEventService>();
        _deviceValidator = Substitute.For<IDeviceValidator>();
        _twoFactorAuthenticationValidator = Substitute.For<ITwoFactorAuthenticationValidator>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _mailService = Substitute.For<IMailService>();
        _logger = Substitute.For<ILogger<BaseRequestValidatorTests>>();
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = Substitute.For<GlobalSettings>();
        _userRepository = Substitute.For<IUserRepository>();
        _policyService = Substitute.For<IPolicyService>();
        _featureService = Substitute.For<IFeatureService>();
        _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        _userDecryptionOptionsBuilder = Substitute.For<IUserDecryptionOptionsBuilder>();

        _sut = new BaseRequestValidatorTestWrapper(
            _userManager,
            _userService,
            _eventService,
            _deviceValidator,
            _twoFactorAuthenticationValidator,
            _organizationUserRepository,
            _mailService,
            _logger,
            _currentContext,
            _globalSettings,
            _userRepository,
            _policyService,
            _featureService,
            _ssoConfigRepository,
            _userDecryptionOptionsBuilder);
    }

    /* Logic path
     * ValidateAsync -> _Logger.LogInformation
     *            |-> BuildErrorResultAsync -> _eventService.LogUserEventAsync
     *                                     |-> SetErrorResult
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_IsBot_UserNotNull_ShouldBuildErrorResult_ShouldLogFailedLoginEvent(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);

        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = true;
        _sut.isValid = true;

        // Act
        await _sut.ValidateAsync(context);

        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];

        // Assert
        await _eventService.Received(1)
                           .LogUserEventAsync(context.CustomValidatorRequestContext.User.Id,
                                             Core.Enums.EventType.User_FailedLogIn);
        Assert.True(context.GrantResult.IsError);
        Assert.Equal("Username or password is incorrect. Try again.", errorResponse.Message);
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
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _globalSettings.Captcha.Returns(new GlobalSettings.CaptchaSettings());
        _globalSettings.SelfHosted = true;
        _sut.isValid = false;

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        _logger.Received(1).LogWarning(Constants.BypassFiltersEventId, "Failed login attempt. ");
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("Username or password is incorrect. Try again.", errorResponse.Message);
    }

    /* Logic path
     * ValidateAsync -> UpdateFailedAuthDetailsAsync -> _mailService.SendFailedLoginAttemptsEmailAsync
     *            |-> BuildErrorResultAsync -> _eventService.LogUserEventAsync
     *                                     |-> SetErrorResult
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_ContextNotValid_MaxAttemptLogin_ShouldSendEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);

        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        // This needs to be n-1 of the max failed login attempts
        context.CustomValidatorRequestContext.User.FailedLoginCount = 2;
        context.CustomValidatorRequestContext.KnownDevice = false;

        _globalSettings.Captcha.Returns(
            new GlobalSettings.CaptchaSettings
            {
                MaximumFailedLoginAttempts = 3
            });
        _sut.isValid = false;

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        await _mailService.Received(1)
                          .SendFailedLoginAttemptsEmailAsync(
                            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal("Username or password is incorrect. Try again.", errorResponse.Message);
    }

    /* Logic path
     * ValidateAsync -> getDeviceFromRequest -> device null -> error result
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceNull_ShouldError(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        requestContext.Device = null;
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        // Act
        await _sut.ValidateAsync(context);

        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];

        // Assert
        Assert.True(context.GrantResult.IsError);
        Assert.Equal("No device information provided.", errorResponse.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceNull_RequestingDeviceIsKnown_ShouldSucceed(
        Device device,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        requestContext.Device = null;
        requestContext.KnownDevice = true;
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        AddValidDeviceToRequest(tokenRequest);
        _deviceValidator.GetKnownDeviceAsync(Arg.Any<User>(), Arg.Any<Device>())
                             .Returns(device);
        _twoFactorAuthenticationValidator.RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceNull_IsNewDeviceRequest_DeviceIsValidated_ShouldSucceed(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        // 2 -> to be new device otp request with valid device in request
        context.CustomValidatorRequestContext.Device = null;
        AddValidDeviceToRequest(context.ValidatedTokenRequest);
        context.ValidatedTokenRequest.Raw["NewDeviceOtp"] = "123456";
        // 3 -> will result to false with no extra configuration
        // 4 -> set two factor to be false
        _twoFactorAuthenticationValidator
                .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 5 -> set up new device otp to pass
        requestContext.KnownDevice = false;
        _globalSettings.EnableNewDeviceVerification = true;
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);
        tokenRequest.GrantType = "password";
        _deviceValidator.HandleNewDeviceVerificationAsync(Arg.Any<User>(), Arg.Any<ValidatedRequest>())
                         .Returns(Task.FromResult((true, "not important")));
        // 6 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        // this check proves that we save a db trip
        await _deviceValidator.Received(0).GetKnownDeviceAsync(Arg.Any<User>(), Arg.Any<Device>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceNull_IsNewDeviceRequest_DeviceNotValidated_ShouldFail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        // 2 -> to be new device otp request with valid device in request
        context.CustomValidatorRequestContext.Device = null;
        AddValidDeviceToRequest(context.ValidatedTokenRequest);
        context.ValidatedTokenRequest.Raw["NewDeviceOtp"] = "123456";
        // 3 -> will result to false with no extra configuration
        // 4 -> set two factor to be false
        _twoFactorAuthenticationValidator
                .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 5 -> set up new device otp to fail
        requestContext.KnownDevice = false;
        _globalSettings.EnableNewDeviceVerification = true;
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);
        tokenRequest.GrantType = "password";
        _deviceValidator.HandleNewDeviceVerificationAsync(Arg.Any<User>(), Arg.Any<ValidatedRequest>())
                         .Returns(Task.FromResult((false, "Invalid Otp")));
        // 6 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        // this check proves that we save a db trip
        await _deviceValidator.Received(0).GetKnownDeviceAsync(Arg.Any<User>(), Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData("client_credential")]
    [BitAutoData("authorization_code")]
    [BitAutoData("webauthn")]
    public async Task ValidateAsync_GrantTypeNotPassword_DeviceNotKnown_ShouldSaveDevice_ShouldSucceed(
        string grantType,
        Device device,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        // 2 -> to be new device otp request with valid device in request
        context.CustomValidatorRequestContext.Device = null;
        AddValidDeviceToRequest(context.ValidatedTokenRequest);
        // 3 -> will result to false with no extra configuration
        // 4 -> set two factor to be false
        _twoFactorAuthenticationValidator
                .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 5 -> set to bypass new device verification based on grant type
        requestContext.KnownDevice = false;
        _globalSettings.EnableNewDeviceVerification = true;
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);
        tokenRequest.GrantType = grantType;
        _deviceValidator.SaveRequestingDeviceAsync(Arg.Any<User>(), Arg.Any<Device>())
            .Returns(device);
        // 6 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        // verify code path did not try to handle new device verification
        await _deviceValidator.Received(0)
            .HandleNewDeviceVerificationAsync(Arg.Any<User>(), Arg.Any<ValidatedRequest>());
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatability")]
    public async Task ValidateAsync_NewDeviceVerificationOff_DeviceNotKnown_ShouldSaveDevice_ShouldSucceed(
    string grantType,
    Device device,
    [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
    CustomValidatorRequestContext requestContext,
    GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        // 1 -> to pass
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        // 2 -> to be new device otp request with valid device in request
        context.CustomValidatorRequestContext.Device = null;
        AddValidDeviceToRequest(context.ValidatedTokenRequest);
        // 3 -> will result to false with no extra configuration
        // 4 -> set two factor to be false
        _twoFactorAuthenticationValidator
                .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
                .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // 5 -> set to not require new device verification
        requestContext.KnownDevice = false;
        _globalSettings.EnableNewDeviceVerification = false;
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(false);
        tokenRequest.GrantType = grantType;
        _deviceValidator.SaveRequestingDeviceAsync(Arg.Any<User>(), tokenRequest)
            .Returns(device);
        // 6 -> not legacy user
        _userService.IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        // verify code path did not try to handle new device verification
        await _deviceValidator.Received(0)
            .HandleNewDeviceVerificationAsync(Arg.Any<User>(), Arg.Any<ValidatedRequest>());
        // Verify code path did not try to save using new method
        await _deviceValidator.Received(0)
            .SaveRequestingDeviceAsync(Arg.Any<User>(), Arg.Any<Device>());
    }

    /* Logic path
     * ValidateAsync -> IsValidAuthTypeAsync -> SaveDeviceAsync -> BuildSuccessResultAsync
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_ClientCredentialsGrantType_ShouldSucceed(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult,
        Device device)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        context.CustomValidatorRequestContext.User.CreationDate = DateTime.UtcNow - TimeSpan.FromDays(1);
        _globalSettings.DisableEmailNewDevice = false;

        context.ValidatedTokenRequest.GrantType = "client_credentials"; // This || AuthCode will allow process to continue to get device

        _deviceValidator.SaveRequestingDeviceAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
                         .Returns(device);
        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
    }

    /* Logic path
     * ValidateAsync -> RequireSsoLoginAsync -> SaveDeviceAsync -> BuildSuccessResultAsync
     */
    [Theory]
    [BitAutoData("client_credentials")]
    [BitAutoData("authorization_code")]
    public async Task ValidateAsync_ClientCredentialsGrantType_ExistingDevice_ShouldSucceed(
        string grantType,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult,
        Device device)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);

        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        context.CustomValidatorRequestContext.User.CreationDate = DateTime.UtcNow - TimeSpan.FromDays(1);
        _globalSettings.DisableEmailNewDevice = false;

        context.ValidatedTokenRequest.GrantType = grantType;

        _deviceValidator.SaveRequestingDeviceAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
                         .Returns(device);
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        // Act
        await _sut.ValidateAsync(context);

        // Assert
        await _eventService.LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await _userRepository.Received(1).ReplaceAsync(Arg.Any<User>());

        Assert.False(context.GrantResult.IsError);
    }

    /* Logic path
     * ValidateAsync -> SSORequired -> BuildErrorResultAsync
     */
    [Theory, BitAutoData]
    public async Task ValidateAsync_InvalidAuthType_ShouldSetSsoResult(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);

        context.ValidatedTokenRequest.Raw["DeviceIdentifier"] = "DeviceIdentifier";
        context.ValidatedTokenRequest.Raw["DevicePushToken"] = "DevicePushToken";
        context.ValidatedTokenRequest.Raw["DeviceName"] = "DeviceName";
        context.ValidatedTokenRequest.Raw["DeviceType"] = "Android"; // This needs to be an actual Type
        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        _sut.isValid = true;

        context.ValidatedTokenRequest.GrantType = "something_invalid";

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

        context.CustomValidatorRequestContext.CaptchaResponse.IsBot = false;
        context.ValidatedTokenRequest.ClientId = "Not Web";
        _sut.isValid = true;
        _featureService.IsEnabled(FeatureFlagKeys.BlockLegacyUsers).Returns(true);
        _twoFactorAuthenticationValidator
            .RequiresTwoFactorAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));

        // Act
        await _sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse["ErrorModel"];
        Assert.Equal($"Encryption key migration is required. Please log in to the web vault at {_globalSettings.BaseServiceUri.VaultWithHash}"
                    , errorResponse.Message);
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

    private ValidatedTokenRequest AddValidDeviceToRequest(ValidatedTokenRequest request)
    {
        request.Raw["DeviceIdentifier"] = "DeviceIdentifier";
        request.Raw["DeviceType"] = "Android"; // must be valid device type
        request.Raw["DeviceName"] = "DeviceName";
        request.Raw["DevicePushToken"] = "DevicePushToken";
        return request;
    }
}
