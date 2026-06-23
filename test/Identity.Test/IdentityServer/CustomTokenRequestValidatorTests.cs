using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
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
    private readonly IFeatureService _featureService;
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
        _featureService = Substitute.For<IFeatureService>();
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
            Substitute.For<IOrganizationUserRepository>(),
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
            _updateDeviceLastActivityCommand);
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

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_NullSubject_SkipsUpdate()
    {
        // Arrange
        var context = CreateRefreshTokenContext(subject: null);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
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
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
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
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
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
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);
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

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
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
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

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

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
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

        var sut = new CustomTokenRequestValidator(
            userManager,
            _userService,
            Substitute.For<IEventService>(),
            Substitute.For<IDeviceValidator>(),
            Substitute.For<ITwoFactorAuthenticationValidator>(),
            Substitute.For<ISsoRequestValidator>(),
            Substitute.For<IOrganizationUserRepository>(),
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
            updateCmd);

        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(Claims.Device, deviceIdentifier),
        ], "test"));
        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

        // Act
        await sut.ValidateAsync(context);

        // Assert: update was called with the version string from CurrentContext
        await updateCmd
            .Received(1)
            .UpdateByIdentifierAndUserIdAsync(deviceIdentifier, userId, "2026.5.1");
    }

    // TODO: PM-34091 - remove this test when cleaning up feature flag (disabled case will no longer exist)
    [Fact]
    public async Task TryUpdateDeviceLastActivityForRefreshAsync_FeatureFlagDisabled_UpdateNotCalledAsync()
    {
        // Arrange
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(Claims.Device, "test-device-identifier"),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: update is skipped — no call made
        Assert.False(context.Result.IsError);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // CurrentContextMiddleware runs before IdentityServer has parsed the refresh token,
    // so DeviceIdentifier and UserId are unset on CurrentContext when this validator runs.
    // The validator must back-fill them from the refresh-token subject before any feature
    // flag evaluation, so progressive (percentage/targeted) rollouts at /connect/token
    // bucket reliably by device and user.
    // TODO: PM-34091 - delete this test and the next when cleaning up the feature flag;
    // they assert against the ??= back-fill that disappears alongside the IsEnabled check.
    [Fact]
    public async Task ValidateAsync_RefreshToken_BackfillsCurrentContextFromSubjectClaims()
    {
        // Arrange — refresh-token subject carries the user id (sub) and device claim.
        // NSubstitute returns string.Empty for unconfigured string properties so set
        // DeviceIdentifier to null explicitly to simulate the middleware-blind state.
        _currentContext.DeviceIdentifier = null;
        var userId = Guid.NewGuid();
        const string deviceIdentifier = "refresh-token-device-id";

        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(Claims.Device, deviceIdentifier),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert — CurrentContext was populated from the subject claims, so any feature
        // flag evaluation downstream gets a properly-keyed LaunchDarkly context.
        Assert.Equal(userId, _currentContext.UserId);
        Assert.Equal(deviceIdentifier, _currentContext.DeviceIdentifier);
    }

    [Fact]
    public async Task ValidateAsync_RefreshToken_DoesNotOverwriteCurrentContext_WhenAlreadyPopulated()
    {
        // Arrange — middleware managed to populate CurrentContext (e.g., the rare client
        // that sends Device-Identifier as an HTTP header). The back-fill must not clobber it.
        var middlewareUserId = Guid.NewGuid();
        const string middlewareDeviceId = "middleware-populated-device-id";
        _currentContext.UserId = middlewareUserId;
        _currentContext.DeviceIdentifier = middlewareDeviceId;

        // Subject claims are different values — they should NOT replace the middleware ones.
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(Claims.Device, "different-device-id-from-token"),
        ], "test"));

        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ValidateAsync(context);

        // Assert — ??= semantics: middleware-populated values win.
        Assert.Equal(middlewareUserId, _currentContext.UserId);
        Assert.Equal(middlewareDeviceId, _currentContext.DeviceIdentifier);
    }

    // Defensive normalization: a whitespace-only `device` claim should not be back-filled
    // and should not trigger a bump. The claim is server-signed at issuance from a non-empty
    // string, so this guards an invariant rather than a known failure mode — but if a token
    // ever surfaces with a blank device claim, we don't want to leak whitespace into
    // CurrentContext.DeviceIdentifier or call the bump command with a blank identifier.
    // TODO: PM-34091 - when cleaning up the feature flag, drop the CurrentContext
    // back-fill assertions (UserId / DeviceIdentifier) and rename to drop "Backfill".
    // The bump-skip assertion still holds — the whitespace normalization on `identifier`
    // remains and continues to gate the unconditional bump.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_RefreshToken_WhitespaceDeviceClaim_DoesNotBackfillOrBump(string deviceClaimValue)
    {
        // Arrange
        _currentContext.DeviceIdentifier = null;
        var userId = Guid.NewGuid();
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(Claims.Device, deviceClaimValue),
        ], "test"));
        var context = CreateRefreshTokenContext(subject);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: refresh succeeds, UserId is back-filled, but DeviceIdentifier is NOT
        // populated with the whitespace string, and the bump is skipped.
        Assert.False(context.Result.IsError);
        Assert.Equal(userId, _currentContext.UserId);
        Assert.Null(_currentContext.DeviceIdentifier);
        await _updateDeviceLastActivityCommand
            .DidNotReceive()
            .UpdateByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }
}
