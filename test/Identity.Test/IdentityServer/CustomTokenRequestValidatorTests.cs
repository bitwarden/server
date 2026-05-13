using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
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
    private readonly IBumpDeviceDataCommand _bumpDeviceDataCommand;
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
        _bumpDeviceDataCommand = Substitute.For<IBumpDeviceDataCommand>();
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
            Substitute.For<ICurrentContext>(),
            Substitute.For<GlobalSettings>(),
            Substitute.For<IUserRepository>(),
            Substitute.For<IPolicyService>(),
            _featureService,
            Substitute.For<ISsoConfigRepository>(),
            Substitute.For<IUserDecryptionOptionsBuilder>(),
            Substitute.For<IUpdateInstallationCommand>(),
            Substitute.For<IPolicyRequirementQuery>(),
            Substitute.For<IAuthRequestRepository>(),
            Substitute.For<IMailService>(),
            Substitute.For<IUserAccountKeysQuery>(),
            Substitute.For<IClientVersionValidator>(),
            _bumpDeviceDataCommand);
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
    public async Task TryBumpDeviceDataForRefreshAsync_NullSubject_SkipsBump()
    {
        // Arrange
        var context = CreateRefreshTokenContext(subject: null);

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);

        // Act
        await _sut.ValidateAsync(context);

        // Assert: bump is skipped — no call made
        Assert.False(context.Result.IsError);
        await _bumpDeviceDataCommand
            .DidNotReceive()
            .BumpByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
    [Fact]
    public async Task TryBumpDeviceDataForRefreshAsync_NoDeviceClaim_SkipsBump()
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

        // Assert: bump is skipped — no call made
        Assert.False(context.Result.IsError);
        await _bumpDeviceDataCommand
            .DidNotReceive()
            .BumpByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
    [Fact]
    public async Task TryBumpDeviceDataForRefreshAsync_InvalidUserIdGuid_SkipsBump()
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

        // Assert: bump is skipped — no call made
        Assert.False(context.Result.IsError);
        await _bumpDeviceDataCommand
            .DidNotReceive()
            .BumpByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
    [Fact]
    public async Task ValidateAsync_BumpByIdentifierThrows_RefreshTokenSucceeds()
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
        _bumpDeviceDataCommand
            .BumpByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns<Task>(_ => throw new Exception("Transient failure"));

        // Act
        await _sut.ValidateAsync(context);

        // Assert: exception is swallowed — token refresh succeeds
        Assert.False(context.Result.IsError);

        // Assert: warning was logged
        var logs = _logger.Collector.GetSnapshot();
        Assert.Contains(logs, l =>
            l.Level == LogLevel.Warning &&
            l.Message.Contains("Failed to bump device data for device with identifier"));
    }

    // TODO: PM-34091 - remove feature flag mock setup when cleaning up feature flag
    [Fact]
    public async Task TryBumpDeviceDataForRefreshAsync_Succeeds_BumpCalledWithCorrectArgsAsync()
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

        // Assert: refresh succeeds and bump was called with the correct identifier and userId.
        // Client version is null because the test setup uses a Substituted ICurrentContext where
        // ClientVersion is unset (default null).
        Assert.False(context.Result.IsError);
        await _bumpDeviceDataCommand
            .Received(1)
            .BumpByIdentifierAndUserIdAsync(deviceIdentifier, userId, null);
    }

    [Fact]
    public async Task TryBumpDeviceDataForRefreshAsync_PassesClientVersionFromContext()
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

        var bumpCmd = Substitute.For<IBumpDeviceDataCommand>();

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
            Substitute.For<IPolicyService>(),
            _featureService,
            Substitute.For<ISsoConfigRepository>(),
            Substitute.For<IUserDecryptionOptionsBuilder>(),
            Substitute.For<IUpdateInstallationCommand>(),
            Substitute.For<IPolicyRequirementQuery>(),
            Substitute.For<IAuthRequestRepository>(),
            Substitute.For<IMailService>(),
            Substitute.For<IUserAccountKeysQuery>(),
            Substitute.For<IClientVersionValidator>(),
            bumpCmd);

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

        // Assert: bump was called with the version string from CurrentContext
        await bumpCmd
            .Received(1)
            .BumpByIdentifierAndUserIdAsync(deviceIdentifier, userId, "2026.5.1");
    }

    // TODO: PM-34091 - remove this test when cleaning up feature flag (disabled case will no longer exist)
    [Fact]
    public async Task TryBumpDeviceDataForRefreshAsync_FeatureFlagDisabled_BumpNotCalledAsync()
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

        // Assert: bump is skipped — no call made
        Assert.False(context.Result.IsError);
        await _bumpDeviceDataCommand
            .DidNotReceive()
            .BumpByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
    }
}
