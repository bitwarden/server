using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidationConstants;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.Test.Wrappers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Identity.Test.IdentityServer;

[SutProviderCustomize]
public class BaseRequestValidatorTests
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    /* Logic path
     * ValidateAsync -> UpdateFailedAuthDetailsAsync -> _mailService.SendFailedLoginAttemptsEmailAsync
     *            |-> BuildErrorResultAsync -> _eventService.LogUserEventAsync
     *                       (self hosted) |-> _logger.LogWarning()
     *                                     |-> SetErrorResult
     */
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ContextNotValid_SelfHosted_ShouldBuildErrorResult_ShouldLogWarning(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var fakeLogger = new FakeLogger<BaseRequestValidatorTests>();
        var sutProvider = GetSutProviderWithFakeLogger(fakeLogger);
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = true;
        sutProvider.Sut.isValid = false;

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        var logs = fakeLogger.Collector.GetSnapshot(true);
        Assert.Contains(logs,
            l => l.Level == LogLevel.Warning && l.Message == "Failed login attempt. Is2FARequest: False IpAddress: ");
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        Assert.Equal("Username or password is incorrect. Try again.", errorResponse.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_DeviceNotValidated_ShouldLogError(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        requestContext.KnownDevice = false;
        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(false));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .IsLegacyUser(Arg.Any<string>())
            .Returns(false);

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(context.CustomValidatorRequestContext.User.Id, EventType.User_FailedLogIn);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_DeviceValidated_ShouldSucceed(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .IsLegacyUser(Arg.Any<string>())
            .Returns(false);
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ValidatedAuthRequest_ConsumedOnSuccess(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

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
            AuthenticationDate = null,
            UserId = requestContext.User.Id,
        };
        requestContext.ValidatedAuthRequest = authRequest;

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .IsLegacyUser(Arg.Any<string>())
            .Returns(false);
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        await sutProvider.GetDependency<IAuthRequestRepository>().Received(1).ReplaceAsync(Arg.Is<AuthRequest>(ar =>
            ar.AuthenticationDate.HasValue));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ValidatedAuthRequest_NotConsumed_When2faRequired(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

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
            AuthenticationDate = null,
            UserId = requestContext.User.Id,
        };
        requestContext.ValidatedAuthRequest = authRequest;
        requestContext.User.TwoFactorProviders =
            "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"user@test.dev\"}}}";

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .BuildTwoFactorResultAsync(requestContext.User, null)
            .Returns(Task.FromResult(new Dictionary<string, object>
            {
                { "TwoFactorProviders", new[] { "0", "1" } },
                { "TwoFactorProviders2", new Dictionary<string, object> { { "Email", null } } }
            }));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        await sutProvider.GetDependency<IAuthRequestRepository>().DidNotReceive().ReplaceAsync(authRequest);
        Assert.Equal("Two-factor authentication required.", context.GrantResult.ErrorDescription);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_TwoFactorTokenInvalid_ShouldSendFailedTwoFactorEmail(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;
        sutProvider.Sut.isValid = true;
        tokenRequest.Raw["TwoFactorToken"] = "invalid_token";
        tokenRequest.Raw["TwoFactorProvider"] = TwoFactorProviderType.Email.ToString();

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.Email, "invalid_token")
            .Returns(Task.FromResult(false));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendFailedTwoFactorAttemptEmailAsync(
                user.Email,
                TwoFactorProviderType.Email,
                Arg.Any<DateTime>(),
                Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_TwoFactorRememberTokenExpired_ShouldNotSendFailedTwoFactorEmail(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;
        sutProvider.Sut.isValid = true;
        tokenRequest.Raw["TwoFactorToken"] = "expired_remember_token";
        tokenRequest.Raw["TwoFactorProvider"] = "5"; // Remember provider

        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(Arg.Any<User>(), tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.Remember, "expired_remember_token")
            .Returns(Task.FromResult(false));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .BuildTwoFactorResultAsync(user, null)
            .Returns(Task.FromResult(new Dictionary<string, object>
            {
                { "TwoFactorProviders", new[] { "0", "1" } },
                { "TwoFactorProviders2", new Dictionary<string, object>() }
            }));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal("Two-factor authentication required.", context.GrantResult.ErrorDescription);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendFailedTwoFactorAttemptEmailAsync(Arg.Any<string>(), Arg.Any<TwoFactorProviderType>(),
                Arg.Any<DateTime>(), Arg.Any<string>());
    }

    // Test grantTypes that require SSO when a user is in an organization that requires it
    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_OrgSsoRequiredTrue_ShouldSetSsoResult(
        string grantType,
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = SsoConstants.RequestErrors.SsoRequired,
            ErrorDescription = SsoConstants.RequestErrors.SsoRequiredDescription
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { CustomResponseConstants.ResponseKeys.ErrorModel, new ErrorResponseModel(SsoConstants.RequestErrors.SsoRequiredDescription) },
        };

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        context.ValidatedTokenRequest.GrantType = grantType;

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(false));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        Assert.NotNull(context.GrantResult.CustomResponse);
        var errorResponse = (ErrorResponseModel)context.CustomValidatorRequestContext.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        Assert.Equal(SsoConstants.RequestErrors.SsoRequiredDescription, errorResponse.Message);
    }

    // Test grantTypes with RequireSsoPolicyRequirement when feature flag is enabled
    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_WithPolicyRequirementsEnabled_OrgSsoRequiredTrue_ShouldSetSsoResult(
        string grantType,
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = SsoConstants.RequestErrors.SsoRequired,
            ErrorDescription = SsoConstants.RequestErrors.SsoRequiredDescription
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { CustomResponseConstants.ResponseKeys.ErrorModel, new ErrorResponseModel(SsoConstants.RequestErrors.SsoRequiredDescription) },
            { CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier, "test-org-identifier" }
        };

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        context.ValidatedTokenRequest.GrantType = grantType;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireSsoPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireSsoPolicyRequirement { SsoRequired = true });
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(false));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        await sutProvider.GetDependency<IPolicyService>().DidNotReceive()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        Assert.True(context.GrantResult.IsError);
        Assert.NotNull(context.GrantResult.CustomResponse);
        var errorResponse = (ErrorResponseModel)context.CustomValidatorRequestContext.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        Assert.Equal(SsoConstants.RequestErrors.SsoRequiredDescription, errorResponse.Message);
    }

    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_WithPolicyRequirementsEnabled_OrgSsoRequiredFalse_ShouldSucceed(
        string grantType,
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        context.ValidatedTokenRequest.GrantType = grantType;
        context.ValidatedTokenRequest.ClientId = "web";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireSsoPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireSsoPolicyRequirement { SsoRequired = false });
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        await sutProvider.GetDependency<IEventService>().Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Any<User>());
    }

    // Test grantTypes where SSO would be required but the user is not in an
    // organization that requires it
    [Theory]
    [BitAutoData("password")]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    public async Task ValidateAsync_GrantTypes_OrgSsoRequiredFalse_ShouldSucceed(
        string grantType,
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        context.ValidatedTokenRequest.GrantType = grantType;
        context.ValidatedTokenRequest.ClientId = "web";

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(false));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        await sutProvider.GetDependency<IEventService>().Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Any<User>());
        Assert.False(context.GrantResult.IsError);
    }

    // Test the grantTypes where SSO is in progress or not relevant
    [Theory]
    [BitAutoData("authorization_code")]
    [BitAutoData("client_credentials")]
    [BitAutoData("client_credentials")]
    public async Task ValidateAsync_GrantTypes_SsoRequiredFalse_ShouldSucceed(
        string grantType,
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        context.ValidatedTokenRequest.GrantType = grantType;
        context.ValidatedTokenRequest.ClientId = "web";

        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        await sutProvider.GetDependency<IPolicyService>().DidNotReceive()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);
        await sutProvider.GetDependency<IEventService>().Received(1).LogUserEventAsync(
            context.CustomValidatorRequestContext.User.Id, EventType.User_LoggedIn);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Any<User>());
        Assert.False(context.GrantResult.IsError);
    }

    /* Logic Path
     * ValidateAsync -> UserService.IsLegacyUser -> FailAuthForLegacyUserAsync
     */
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_IsLegacyUser_FailAuthForLegacyUserAsync(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = context.CustomValidatorRequestContext.User;
        user.Key = null;
        context.ValidatedTokenRequest.ClientId = "Not Web";
        sutProvider.Sut.isValid = true;

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(Arg.Any<User>(), Arg.Any<ValidatedTokenRequest>())
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        var expectedMessage =
            "Legacy encryption without a userkey is no longer supported. To recover your account, please contact support";
        Assert.Equal(expectedMessage, errorResponse.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_CustomResponse_NoMasterPassword_ShouldSetUserDecryptionOptions(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

        var userDecryptionOptionsBuilder = sutProvider.GetDependency<IUserDecryptionOptionsBuilder>();
        userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
        {
            HasMasterPassword = false,
            MasterPasswordUnlock = null
        }));

        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

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
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

        var userDecryptionOptionsBuilder = sutProvider.GetDependency<IUserDecryptionOptionsBuilder>();
        userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
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

        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

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
    [BitAutoData]
    public async Task ValidateAsync_CustomResponse_ShouldIncludeAccountKeys(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

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
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(mockAccountKeys);

        var userDecryptionOptionsBuilder = sutProvider.GetDependency<IUserDecryptionOptionsBuilder>();
        userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions
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

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        var customResponse = context.GrantResult.CustomResponse;

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
    [BitAutoData]
    public async Task ValidateAsync_CustomResponse_AccountKeysQuery_SkippedWhenPrivateKeyIsNull(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        requestContext.User.PrivateKey = null;
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        await sutProvider.GetDependency<IUserAccountKeysQuery>().Received(0).Run(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_CustomResponse_AccountKeysQuery_CalledWithCorrectUser(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var expectedUser = requestContext.User;
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;

        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        var userDecryptionOptionsBuilder = sutProvider.GetDependency<IUserDecryptionOptionsBuilder>();
        userDecryptionOptionsBuilder.ForUser(Arg.Any<User>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithDevice(Arg.Any<Device>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithSso(Arg.Any<SsoConfig>()).Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.WithWebAuthnLoginCredential(Arg.Any<WebAuthnCredential>())
            .Returns(userDecryptionOptionsBuilder);
        userDecryptionOptionsBuilder.BuildAsync().Returns(Task.FromResult(new UserDecryptionOptions()));

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(false, null)));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError);
        await sutProvider.GetDependency<IUserAccountKeysQuery>().Received(1)
            .Run(Arg.Is<User>(u => u.Id == expectedUser.Id));
    }

    /// <summary>
    /// Tests the core PM-21153 feature: SSO-required users can use recovery codes to disable 2FA,
    /// but must then authenticate via SSO with a descriptive message about the recovery.
    /// This test validates:
    /// 1. Validation order prioritizes 2FA before SSO when recovery code is provided
    /// 2. Recovery code successfully validates and sets TwoFactorRecoveryRequested flag
    /// 3. SSO validation then fails with recovery-specific message
    /// 4. User is NOT logged in (must authenticate via IdP)
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_RecoveryCodeForSsoRequiredUser_BlocksWithDescriptiveMessage(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = SsoConstants.RequestErrors.SsoRequired,
            ErrorDescription = SsoConstants.RequestErrors.SsoRequiredDescription
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { CustomResponseConstants.ResponseKeys.ErrorModel, new ErrorResponseModel(SsoConstants.RequestErrors.SsoRequiredDescription) },
        };

        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;
        requestContext.TwoFactorRecoveryRequested = false;
        requestContext.RememberMeRequested = false;
        sutProvider.Sut.isValid = true;
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code-12345";

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(user, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code-12345")
            .Returns(Task.FromResult(true));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError, "Authentication should fail - SSO required after recovery");
        Assert.NotNull(context.GrantResult.CustomResponse);
        var errorResponse = (ErrorResponseModel)context.CustomValidatorRequestContext.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        Assert.Equal(
            SsoConstants.RequestErrors.SsoRequiredDescription,
            errorResponse.Message);
        Assert.True(requestContext.TwoFactorRecoveryRequested,
            "TwoFactorRecoveryRequested flag should be set");
        await sutProvider.GetDependency<IEventService>().DidNotReceive()
            .LogUserEventAsync(user.Id, EventType.User_LoggedIn);
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
    [BitAutoData]
    public async Task ValidateAsync_InvalidRecoveryCodeForSsoRequiredUser_FailsAt2FA(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;
        sutProvider.Sut.isValid = true;
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "INVALID-recovery-code";

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(user, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.RecoveryCode, "INVALID-recovery-code")
            .Returns(Task.FromResult(false));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError, "Authentication should fail - invalid recovery code");
        var errorResponse = (ErrorResponseModel)context.GrantResult.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        Assert.Equal(
            "Two-step token is invalid. Try again.",
            errorResponse.Message);
        Assert.False(requestContext.TwoFactorRecoveryRequested,
            "TwoFactorRecoveryRequested should be false (recovery failed)");
        await sutProvider.GetDependency<IMailService>().Received(1).SendFailedTwoFactorAttemptEmailAsync(
            user.Email,
            TwoFactorProviderType.RecoveryCode,
            Arg.Any<DateTime>(),
            Arg.Any<string>());
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_FailedLogIn2fa);
        await sutProvider.GetDependency<IEventService>().DidNotReceive()
            .LogUserEventAsync(user.Id, EventType.User_LoggedIn);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id && u.FailedLoginCount > 0));
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
    [BitAutoData]
    public async Task ValidateAsync_RecoveryCodeForNonSsoUser_SuccessfulLogin(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        var user = requestContext.User;
        sutProvider.Sut.isValid = true;
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code-67890";

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(
                Arg.Any<Guid>(), PolicyType.RequireSso, OrganizationUserStatusType.Confirmed)
            .Returns(Task.FromResult(false));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(user, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .VerifyTwoFactorAsync(user, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code-67890")
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IDeviceValidator>()
            .ValidateRequestDeviceAsync(tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .IsLegacyUser(Arg.Any<string>())
            .Returns(false);
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(requestContext.User, tokenRequest, requestContext)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(Arg.Any<User>()).Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                    "test-private-key",
                    "test-public-key"
                )
            });

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.GrantResult.IsError,
            "Authentication should succeed for non-SSO user with valid recovery code");
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_LoggedIn);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id && u.FailedLoginCount == 0));
        Assert.True(requestContext.TwoFactorRecoveryRequested,
            "TwoFactorRecoveryRequested flag should be set for audit/logging");
    }

    /// <summary>
    /// Tests that when SSO validation returns a custom response, (e.g., with organization identifier),
    /// that custom response is properly propagated to the result.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_SsoRequired_PropagatesCustomResponse(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        sutProvider.Sut.isValid = true;
        tokenRequest.GrantType = OidcConstants.GrantTypes.Password;
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = SsoConstants.RequestErrors.SsoRequired,
            ErrorDescription = SsoConstants.RequestErrors.SsoRequiredDescription
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { CustomResponseConstants.ResponseKeys.ErrorModel, new ErrorResponseModel(SsoConstants.RequestErrors.SsoRequiredDescription) },
            { CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier, "test-org-identifier" }
        };

        var context = CreateContext(tokenRequest, requestContext, grantResult);

        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(
                Arg.Any<User>(),
                Arg.Any<ValidatedTokenRequest>(),
                Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(false));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        Assert.NotNull(context.GrantResult.CustomResponse);
        Assert.Contains(CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier, context.CustomValidatorRequestContext.CustomResponse);
        Assert.Equal("test-org-identifier",
            context.CustomValidatorRequestContext.CustomResponse[CustomResponseConstants.ResponseKeys.SsoOrganizationIdentifier]);
    }

    /// <summary>
    /// Tests that when a recovery code is used for SSO-required user,
    /// the SsoRequestValidator provides the recovery-specific error message.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_RecoveryWithSso_CorrectValidatorMessage(
        SutProvider<BaseRequestValidatorTestWrapper> sutProvider,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        [AuthFixtures.CustomValidatorRequestContext]
        CustomValidatorRequestContext requestContext,
        GrantValidationResult grantResult)
    {
        // Arrange
        var context = CreateContext(tokenRequest, requestContext, grantResult);
        sutProvider.Sut.isValid = true;
        tokenRequest.Raw["TwoFactorProvider"] = ((int)TwoFactorProviderType.RecoveryCode).ToString();
        tokenRequest.Raw["TwoFactorToken"] = "valid-recovery-code";
        requestContext.TwoFactorRecoveryRequested = true;
        requestContext.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = SsoConstants.RequestErrors.SsoRequired,
            ErrorDescription = SsoConstants.RequestErrors.SsoTwoFactorRecoveryDescription
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            {
                CustomResponseConstants.ResponseKeys.ErrorModel,
                new ErrorResponseModel(SsoConstants.RequestErrors.SsoTwoFactorRecoveryDescription)
            }
        };

        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .RequiresTwoFactorAsync(requestContext.User, tokenRequest)
            .Returns(Task.FromResult(new Tuple<bool, Organization>(true, null)));
        sutProvider.GetDependency<ITwoFactorAuthenticationValidator>()
            .VerifyTwoFactorAsync(requestContext.User, null, TwoFactorProviderType.RecoveryCode, "valid-recovery-code")
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<ISsoRequestValidator>()
            .ValidateAsync(
                Arg.Any<User>(),
                Arg.Any<ValidatedTokenRequest>(),
                Arg.Any<CustomValidatorRequestContext>())
            .Returns(Task.FromResult(false));

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.GrantResult.IsError);
        var errorResponse = (ErrorResponseModel)context.CustomValidatorRequestContext.CustomResponse[CustomResponseConstants.ResponseKeys.ErrorModel];
        Assert.Equal(SsoConstants.RequestErrors.SsoTwoFactorRecoveryDescription, errorResponse.Message);
    }

    private static BaseRequestValidationContextFake CreateContext(
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

    /// <summary>
    /// Creates a SutProvider with a FakeLogger for tests that need to verify log output.
    /// </summary>
    private static SutProvider<BaseRequestValidatorTestWrapper> GetSutProviderWithFakeLogger(
        FakeLogger<BaseRequestValidatorTests> fakeLogger)
    {
        return new SutProvider<BaseRequestValidatorTestWrapper>()
            .SetDependency<ILogger>(fakeLogger)
            .Create();
    }
}
