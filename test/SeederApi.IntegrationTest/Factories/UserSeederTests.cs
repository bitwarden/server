using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class UserSeederTests
{
    private const string _email = "jim.halpert@dundermifflin.test";

    private static readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

    [Fact]
    public void Create_WithoutKeys_ManglesEmail()
    {
        var (user, _) = UserSeeder.Create(new UserSeed { Email = _email }, _passwordHasher, new ManglerService());

        Assert.NotEqual(_email, user.Email);
        Assert.Matches(@"^[a-f0-9]{8}\+jim\.halpert@dundermifflin\.test$", user.Email);
    }

    [Fact]
    public void Create_WithKeys_DoesNotMangleEmail()
    {
        // The caller pre-mangles and pre-generates keys; a second mangle would desync email from key material.
        var keys = RustSdkService.GenerateUserKeys(_email, UserSeeder.DefaultPassword);

        var (user, returnedKeys) = UserSeeder.Create(
            new UserSeed { Email = _email, Keys = keys }, _passwordHasher, new ManglerService());

        Assert.Equal(_email, user.Email);
        Assert.Same(keys, returnedKeys);
    }

    [Fact]
    public void Create_SetsBillingGatewayIdentifiers()
    {
        var (user, _) = UserSeeder.Create(
            new UserSeed
            {
                Email = _email,
                Gateway = GatewayType.Stripe,
                GatewayCustomerId = "cus_test123",
                GatewaySubscriptionId = "sub_test123"
            },
            _passwordHasher,
            new NoOpManglerService());

        Assert.Equal(GatewayType.Stripe, user.Gateway);
        Assert.Equal("cus_test123", user.GatewayCustomerId);
        Assert.Equal("sub_test123", user.GatewaySubscriptionId);
    }

    [Fact]
    public void Create_WhenPremium_SetsExpirationOneYearOut()
    {
        var before = DateTime.UtcNow;

        var (user, _) = UserSeeder.Create(
            new UserSeed { Email = _email, Premium = true, MaxStorageGb = 1 },
            _passwordHasher,
            new NoOpManglerService());

        Assert.True(user.Premium);
        Assert.NotNull(user.PremiumExpirationDate);
        Assert.InRange(
            user.PremiumExpirationDate!.Value,
            before.AddYears(1),
            DateTime.UtcNow.AddYears(1));
    }

    [Fact]
    public void Create_WhenNotPremium_LeavesExpirationNull()
    {
        var (user, _) = UserSeeder.Create(new UserSeed { Email = _email }, _passwordHasher, new NoOpManglerService());

        Assert.False(user.Premium);
        Assert.Null(user.PremiumExpirationDate);
    }

    [Fact]
    public void Create_WithCreationDate_BackdatesCreationDate()
    {
        var aged = DateTime.UtcNow.AddDays(-365);

        var (user, _) = UserSeeder.Create(
            new UserSeed { Email = _email, CreationDate = aged },
            _passwordHasher,
            new NoOpManglerService());

        Assert.Equal(aged, user.CreationDate);
    }

    [Fact]
    public void Create_WithoutCreationDate_LeavesDatesAtNow()
    {
        var before = DateTime.UtcNow;

        var (user, _) = UserSeeder.Create(new UserSeed { Email = _email }, _passwordHasher, new NoOpManglerService());

        var after = DateTime.UtcNow;
        Assert.InRange(user.CreationDate, before, after);
        Assert.InRange(user.RevisionDate, before, after);
        Assert.InRange(user.AccountRevisionDate, before, after);
    }

    [Fact]
    public void Create_WithCreationDate_DoesNotBackdateRevisionDates()
    {
        var before = DateTime.UtcNow;
        var aged = before.AddDays(-365);

        var (user, _) = UserSeeder.Create(
            new UserSeed { Email = _email, CreationDate = aged },
            _passwordHasher,
            new NoOpManglerService());

        var after = DateTime.UtcNow;
        Assert.InRange(user.RevisionDate, before, after);
        Assert.InRange(user.AccountRevisionDate, before, after);
    }

    [Fact]
    public void Create_NullName_DefaultsToEmailLocalPart()
    {
        var (user, _) = UserSeeder.Create(new UserSeed { Email = _email }, _passwordHasher, new NoOpManglerService());

        Assert.Equal("jim.halpert", user.Name);
    }

    [Fact]
    public void Create_SetsProfileAndAuthProperties()
    {
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Authenticator] = new() { Enabled = true }
        };

        var (user, _) = UserSeeder.Create(
            new UserSeed
            {
                Email = _email,
                Name = "Jim Halpert",
                MasterPasswordHint = "the office",
                Culture = "fr-FR",
                AvatarColor = "#4a90d9",
                ForcePasswordReset = true,
                UsesKeyConnector = true,
                TwoFactorProviders = twoFactorProviders
            },
            _passwordHasher,
            new NoOpManglerService());

        Assert.Equal("Jim Halpert", user.Name);
        Assert.Equal("the office", user.MasterPasswordHint);
        Assert.Equal("fr-FR", user.Culture);
        Assert.Equal("#4a90d9", user.AvatarColor);
        Assert.True(user.ForcePasswordReset);
        Assert.True(user.UsesKeyConnector);
        Assert.True(user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator)?.Enabled);

        // A null Culture must leave the entity's own default in place rather than overwriting it.
        var (defaulted, _) = UserSeeder.Create(
            new UserSeed { Email = _email }, _passwordHasher, new NoOpManglerService());

        Assert.Equal("en-US", defaulted.Culture);
        Assert.Null(defaulted.MasterPasswordHint);
        Assert.Null(defaulted.AvatarColor);
        Assert.False(defaulted.ForcePasswordReset);
        Assert.False(defaulted.UsesKeyConnector);
        Assert.Null(defaulted.TwoFactorProviders);
    }
}
