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
    private readonly IBumpDeviceLastActivityDateCommand _bumpDeviceLastActivityDateCommand;
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
        _bumpDeviceLastActivityDateCommand = Substitute.For<IBumpDeviceLastActivityDateCommand>();
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
            _bumpDeviceLastActivityDateCommand);
    }

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

        var context = new CustomTokenRequestValidationContext
        {
            Result = new TokenRequestValidationResult(validatedRequest)
        };

        _userService.IsLegacyUser(Arg.Any<string>()).Returns(false);
        _featureService.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);
        _bumpDeviceLastActivityDateCommand
            .BumpByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns<Task>(_ => throw new Exception("Transient failure"));

        // Act
        await _sut.ValidateAsync(context);

        // Assert: exception is swallowed — token refresh succeeds
        Assert.False(context.Result.IsError);

        // Assert: warning was logged
        var logs = _logger.Collector.GetSnapshot();
        Assert.Contains(logs, l =>
            l.Level == LogLevel.Warning &&
            l.Message.Contains("Failed to bump LastActivityDate for device with identifier"));
    }
}
