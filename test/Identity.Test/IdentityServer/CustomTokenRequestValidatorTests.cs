using System.Reflection;
using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class CustomTokenRequestValidatorTests
{
    private readonly IUserService _userService;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IUpdateDeviceLastActivityCommand _updateDeviceLastActivityCommand;
    private readonly ICurrentContext _currentContext;
    private readonly FakeLogger<CustomTokenRequestValidator> _logger;

    private readonly CustomTokenRequestValidator _sut;

    public CustomTokenRequestValidatorTests()
    {
        var userManager = new UserManager<User>(
            Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());

        _userService = Substitute.For<IUserService>();
        _featureService = Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();
        _updateDeviceLastActivityCommand = Substitute.For<IUpdateDeviceLastActivityCommand>();
        _currentContext = Substitute.For<ICurrentContext>();
        _logger = new FakeLogger<CustomTokenRequestValidator>();

        _sut = new CustomTokenRequestValidator(
            userManager,
            _userService,
            Substitute.For<IEventService>(),
            Substitute.For<IDeviceValidator>(),
            Substitute.For<ITwoFactorAuthenticationValidator>(),
            Substitute.For<ISsoRequestValidator>(),
            _logger,
            _currentContext,
            Substitute.For<GlobalSettings>(),
            Substitute.For<IUserRepository>(),
            _featureService,
            Substitute.For<ISsoConfigRepository>(),
            Substitute.For<IUserDecryptionOptionsBuilder>(),
            Substitute.For<IUpdateInstallationCommand>(),
            Substitute.For<IPolicyRequirementQuery>(),
            Substitute.For<IAuthRequestRepository>(),
            Substitute.For<IMailService>(),
            Substitute.For<IUserAccountKeysQuery>(),
            Substitute.For<IClientVersionValidator>(),
            _updateDeviceLastActivityCommand,
            Substitute.For<IIssueTwoFactorRememberTokenCommand>());
    }

    private CustomTokenRequestValidationContext CreateRefreshTokenContext(ClaimsPrincipal subject)
    {
        var validatedRequest = new ValidatedTokenRequest
        {
            GrantType = "refresh_token",
            Subject = subject,
            ClientId = "web",
            Client = new Client
            {
                AllowedScopes = new HashSet<string>(),
                Properties = new Dictionary<string, string>()
            },
            ClientClaims = []
        };
        return new CustomTokenRequestValidationContext
        {
            Result = new TokenRequestValidationResult(validatedRequest)
        };
    }

    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_NullSubject_SkipsUpdate()
    {
        // Arrange
        var context = CreateRefreshTokenContext(subject: null);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_NoDeviceClaim_SkipsUpdate()
    {
        // Arrange — subject has a valid user ID but no device claim
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString()),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_InvalidUserIdGuid_SkipsUpdate()
    {
        // Arrange — subject has a device claim but the sub claim is not a valid GUID
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, "not-a-guid"),
            new Claim(Claims.Device, "test-device-identifier"),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ValidateAsync_UpdateByIdentifierThrows_RefreshTokenSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceIdentifier = "test-device-identifier";

        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(Claims.Device, deviceIdentifier),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _updateDeviceLastActivityCommand
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns<Task>(_ => throw new Exception("Transient failure"));

        // Act
        await _sut.ValidateAsync(context);

        // Assert: exception is swallowed — token refresh succeeds
        Assert.False(context.Result.IsError);

        // Assert: warning was logged
        var logs = _logger.Collector.GetSnapshot();
        Assert.Contains(logs, l =>
            l.Level == LogLevel.Warning &&
            l.Message.Contains("Failed to update device last activity for device with identifier"));
    }

    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_Succeeds_UpdateCalledWithCorrectArgsAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceIdentifier = "test-device-identifier";

        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(Claims.Device, deviceIdentifier),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: refresh succeeds and update was called with the correct identifier and userId.
        // Client version is null because the test setup uses a Substituted ICurrentContext where
        // ClientVersion is unset (default null).
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .Received(1)
            .UpdateByIdentifierAndUserIdAsync(deviceIdentifier, userId, null);
    }

    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_PassesClientVersionFromContext()
    {
        // Arrange — substitute a CurrentContext with a non-null ClientVersion
        var userId = Guid.NewGuid();
        var deviceIdentifier = "test-device-identifier";
        var clientVersion = new Version("2026.5.1");

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.ClientVersion.Returns(clientVersion);

        var userManager = new UserManager<User>(
            Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());

        var updateCmd = Substitute.For<IUpdateDeviceLastActivityCommand>();
        var issueRememberCmd = Substitute.For<IIssueTwoFactorRememberTokenCommand>();

        var sut = new CustomTokenRequestValidator(
            userManager,
            _userService,
            Substitute.For<IEventService>(),
            Substitute.For<IDeviceValidator>(),
            Substitute.For<ITwoFactorAuthenticationValidator>(),
            Substitute.For<ISsoRequestValidator>(),
            _logger,
            currentContext,
            Substitute.For<GlobalSettings>(),
            Substitute.For<IUserRepository>(),
            _featureService,
            Substitute.For<ISsoConfigRepository>(),
            Substitute.For<IUserDecryptionOptionsBuilder>(),
            Substitute.For<IUpdateInstallationCommand>(),
            Substitute.For<IPolicyRequirementQuery>(),
            Substitute.For<IAuthRequestRepository>(),
            Substitute.For<IMailService>(),
            Substitute.For<IUserAccountKeysQuery>(),
            Substitute.For<IClientVersionValidator>(),
            updateCmd,
            issueRememberCmd);

        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(Claims.Device, deviceIdentifier),
        ], "test"));
        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await sut.ValidateAsync(context);

        // Assert: update was called with the version string from CurrentContext
        await updateCmd
            .Received(1)
            .UpdateByIdentifierAndUserIdAsync(deviceIdentifier, userId, "2026.5.1");
    }

    // Defensive normalization: a whitespace-only `device` claim must not trigger a bump.
    // The claim is server-signed at issuance from a non-empty string, so this guards an
    // invariant rather than a known failure mode.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_RefreshToken_WhitespaceDeviceClaim_DoesNotBump(string deviceClaimValue)
    {
        // Arrange
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(Claims.Device, deviceClaimValue),
        ], "test"));
        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: refresh succeeds and the bump is skipped.
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData("apikey", ApiScopes.ApiSecrets)]
    [InlineData("access-connector", ApiScopes.ApiPamCredentialRotation)]
    public async Task ValidateAsync_ClientCredentialsWithMachineScope_ReturnsEncryptedPayload(string clientIdPrefix,
        string scope)
    {
        // Arrange
        var validatedRequest = new ValidatedTokenRequest
        {
            GrantType = "client_credentials",
            ClientId = $"{clientIdPrefix}.{Guid.NewGuid()}",
            Client = new Client
            {
                AllowedScopes = new HashSet<string> { scope },
                Properties = new Dictionary<string, string> { { "encryptedPayload", "encrypted-payload" } }
            },
            ClientClaims = []
        };
        var context = new CustomTokenRequestValidationContext
        {
            Result = new TokenRequestValidationResult(validatedRequest)
        };

        // Act
        await _sut.ValidateAsync(context);

        // Assert: user validation is skipped and the client's encrypted payload is returned
        Assert.False(context.Result.IsError);
        Assert.Equal("encrypted-payload", context.Result.CustomResponse["encrypted_payload"]);
    }

    [Fact]
    public async Task SetSuccessResult_WithSubject_AddsClaimsToSubject()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, userId.ToString())], "Duende.IdentityServer"));
        var context = CreateTokenRequestContext("authorization_code", subject);
        var claims = CreateSubjectClaims();

        // Act
        await InvokeSetSuccessResultAsync(context, new User { Id = userId, MasterPassword = "hash" }, claims);

        // Assert
        Assert.Equal("stamp", subject.FindFirstValue(Claims.SecurityStamp));
        Assert.Equal("device-identifier", subject.FindFirstValue(Claims.Device));
        Assert.Equal(DeviceType.ChromeBrowser.ToString(), subject.FindFirstValue(Claims.DeviceType));
        Assert.Empty(context.Result!.ValidatedRequest.ClientClaims);
        Assert.False(context.Result.ValidatedRequest.Client.AlwaysSendClientClaims);
    }

    [Fact]
    public async Task SetSuccessResult_WithoutSubject_AddsClaimsAsClientClaims()
    {
        // Arrange
        var context = CreateTokenRequestContext("client_credentials", subject: null);
        var claims = CreateSubjectClaims();

        // Act
        await InvokeSetSuccessResultAsync(context, new User { Id = Guid.NewGuid(), MasterPassword = "hash" }, claims);

        // Assert
        var validatedRequest = context.Result!.ValidatedRequest;
        Assert.Equal(claims, validatedRequest.ClientClaims);
        Assert.True(validatedRequest.Client.AlwaysSendClientClaims);
        Assert.Equal(string.Empty, validatedRequest.Client.ClientClaimsPrefix);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetSuccessResult_NoClaims_LeavesTokenClaimsUnchanged(bool claimsIsNull)
    {
        // Arrange
        var context = CreateTokenRequestContext("client_credentials", subject: null);
        var client = context.Result!.ValidatedRequest.Client;
        var originalPrefix = client.ClientClaimsPrefix;
        List<Claim>? claims = claimsIsNull ? null : [];

        // Act
        await InvokeSetSuccessResultAsync(context, new User { Id = Guid.NewGuid(), MasterPassword = "hash" }, claims);

        // Assert
        Assert.Empty(context.Result.ValidatedRequest.ClientClaims);
        Assert.False(client.AlwaysSendClientClaims);
        Assert.Equal(originalPrefix, client.ClientClaimsPrefix);
    }

    private static CustomTokenRequestValidationContext CreateTokenRequestContext(string grantType, ClaimsPrincipal? subject)
    {
        var validatedRequest = new ValidatedTokenRequest
        {
            GrantType = grantType,
            Subject = subject,
            ClientId = "web",
            Client = new Client
            {
                AllowedScopes = new HashSet<string>(),
                Properties = new Dictionary<string, string>()
            },
            ClientClaims = []
        };
        return new CustomTokenRequestValidationContext
        {
            Result = new TokenRequestValidationResult(validatedRequest)
        };
    }

    private static List<Claim> CreateSubjectClaims() =>
    [
        new Claim(Claims.SecurityStamp, "stamp"),
        new Claim(Claims.Device, "device-identifier"),
        new Claim(Claims.DeviceType, DeviceType.ChromeBrowser.ToString()),
    ];

    private Task InvokeSetSuccessResultAsync(CustomTokenRequestValidationContext context, User user, List<Claim>? claims)
    {
        var methodInfo = typeof(CustomTokenRequestValidator).GetMethod(
            "SetSuccessResult", BindingFlags.NonPublic | BindingFlags.Instance);
        if (methodInfo == null)
        {
            throw new InvalidOperationException("Method 'SetSuccessResult' not found.");
        }

        return (Task)methodInfo.Invoke(_sut, [context, user, claims, new Dictionary<string, object>()])!;
    }
}
