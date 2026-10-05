using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
public class TwoFactorRememberTokenableTests
{
    private static DataProtectorTokenFactory<TwoFactorRememberTokenable> GetSigningFactory()
    {
        var fixture = new Fixture();
        return new DataProtectorTokenFactory<TwoFactorRememberTokenable>(
            TwoFactorRememberTokenable.ClearTextPrefix,
            TwoFactorRememberTokenable.DataProtectorPurpose,
            fixture.Create<EphemeralDataProtectionProvider>(),
            Substitute.For<ILogger<DataProtectorTokenFactory<TwoFactorRememberTokenable>>>());
    }

    private static TwoFactorRememberTokenable NewTokenable(
        Guid? userId = null,
        Guid? deviceId = null,
        string deviceIdentifier = "device-identifier",
        string stamp = "row-stamp",
        string securityStamp = "user-security-stamp") =>
        new()
        {
            UserId = userId ?? Guid.NewGuid(),
            DeviceId = deviceId ?? Guid.NewGuid(),
            DeviceIdentifier = deviceIdentifier,
            Stamp = stamp,
            SecurityStamp = securityStamp,
            ExpirationDate = DateTime.UtcNow.AddDays(30),
        };

    /// <summary>
    /// A token constructed without going through the factory has no expiry, so it is expired and never
    /// valid. Callers that bypass the factory fail closed instead of getting an ad-hoc lifetime.
    /// </summary>
    [Fact]
    public void Constructor_WithoutFactory_IsExpiredAndInvalid()
    {
        var token = new TwoFactorRememberTokenable();

        Assert.Equal(default, token.ExpirationDate);
        Assert.True(token.IsExpired);
        Assert.False(token.Valid);
    }

    [Fact]
    public void InternalConstructor_ValidArguments_BindsEveryField()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var token = new TwoFactorRememberTokenable(userId, deviceId, "device-identifier", "row-stamp", "user-stamp");

        Assert.Equal(userId, token.UserId);
        Assert.Equal(deviceId, token.DeviceId);
        Assert.Equal("device-identifier", token.DeviceIdentifier);
        Assert.Equal("row-stamp", token.Stamp);
        Assert.Equal("user-stamp", token.SecurityStamp);
        Assert.Equal(TwoFactorRememberTokenable.TokenIdentifier, token.Identifier);
    }

    [Fact]
    public void InternalConstructor_DefaultUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TwoFactorRememberTokenable(default, Guid.NewGuid(), "device-identifier", "row-stamp", "user-stamp"));
    }

    [Fact]
    public void InternalConstructor_DefaultDeviceId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TwoFactorRememberTokenable(Guid.NewGuid(), default, "device-identifier", "row-stamp", "user-stamp"));
    }

    [Theory]
    [InlineData("", "row-stamp", "user-stamp")]
    [InlineData(" ", "row-stamp", "user-stamp")]
    [InlineData("device-identifier", "", "user-stamp")]
    [InlineData("device-identifier", " ", "user-stamp")]
    [InlineData("device-identifier", "row-stamp", "")]
    [InlineData("device-identifier", "row-stamp", " ")]
    public void InternalConstructor_BlankStringArgument_Throws(
        string deviceIdentifier, string stamp, string securityStamp)
    {
        Assert.Throws<ArgumentException>(() =>
            new TwoFactorRememberTokenable(Guid.NewGuid(), Guid.NewGuid(), deviceIdentifier, stamp, securityStamp));
    }

    [Fact]
    public void ValidateTwoFactorRememberToken_ValidToken_ReturnsNullAndTheToken()
    {
        var factory = GetSigningFactory();
        var token = NewTokenable();

        var error = TwoFactorRememberTokenable.ValidateTwoFactorRememberToken(
            factory, factory.Protect(token), out var recovered);

        Assert.Null(error);
        Assert.NotNull(recovered);
        Assert.Equal(token.UserId, recovered.UserId);
    }

    [Fact]
    public void ValidateTwoFactorRememberToken_UnprotectFails_ReturnsInvalidToken()
    {
        var error = TwoFactorRememberTokenable.ValidateTwoFactorRememberToken(
            GetSigningFactory(), "not-a-token", out var recovered);

        Assert.Equal(TokenableValidationError.InvalidToken, error);
        Assert.Null(recovered);
    }

    [Fact]
    public void ValidateTwoFactorRememberToken_ExpiredToken_ReturnsExpired()
    {
        var factory = GetSigningFactory();
        var token = NewTokenable();
        token.ExpirationDate = DateTime.UtcNow.AddMinutes(-1);

        var error = TwoFactorRememberTokenable.ValidateTwoFactorRememberToken(
            factory, factory.Protect(token), out _);

        Assert.Equal(TokenableValidationError.ExpiringTokenables.Expired, error);
    }

    [Fact]
    public void ValidateTwoFactorRememberToken_MissingField_ReturnsInvalidToken()
    {
        var factory = GetSigningFactory();
        var token = NewTokenable(stamp: " ");

        var error = TwoFactorRememberTokenable.ValidateTwoFactorRememberToken(
            factory, factory.Protect(token), out _);

        Assert.Equal(TokenableValidationError.InvalidToken, error);
    }

    /// <summary>
    /// The binding properties have internal setters, so this round trip is also what proves
    /// <c>[JsonInclude]</c> is doing its job: without it every field would come back as its default.
    /// </summary>
    [Fact]
    public void ProtectUnprotect_ValidToken_PreservesDataAndExpiration()
    {
        var factory = GetSigningFactory();
        var token = NewTokenable();
        var originalExpiration = token.ExpirationDate;

        var recovered = factory.Unprotect(factory.Protect(token));

        Assert.Equal(token.UserId, recovered.UserId);
        Assert.Equal(token.DeviceId, recovered.DeviceId);
        Assert.Equal(token.DeviceIdentifier, recovered.DeviceIdentifier);
        Assert.Equal(token.Stamp, recovered.Stamp);
        Assert.Equal(token.SecurityStamp, recovered.SecurityStamp);
        Assert.Equal(TwoFactorRememberTokenable.TokenIdentifier, recovered.Identifier);
        Assert.Equal(originalExpiration, recovered.ExpirationDate, TimeSpan.FromSeconds(1));
        Assert.True(recovered.Valid);
    }

    [Fact]
    public void Protect_Always_AppliesClearTextPrefix()
    {
        var factory = GetSigningFactory();

        var protectedToken = factory.Protect(NewTokenable());

        Assert.StartsWith(TwoFactorRememberTokenable.ClearTextPrefix, protectedToken);
    }

    [Fact]
    public void Valid_Expired_ReturnsFalse()
    {
        var token = NewTokenable();
        token.ExpirationDate = DateTime.UtcNow.AddMinutes(-1);

        Assert.True(token.IsExpired);
        Assert.False(token.Valid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Valid_MissingDeviceIdentifier_ReturnsFalse(string? deviceIdentifier)
    {
        var token = NewTokenable(deviceIdentifier: deviceIdentifier!);

        Assert.False(token.Valid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Valid_MissingStamp_ReturnsFalse(string? stamp)
    {
        var token = NewTokenable(stamp: stamp!);

        Assert.False(token.Valid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Valid_MissingSecurityStamp_ReturnsFalse(string? securityStamp)
    {
        var token = NewTokenable(securityStamp: securityStamp!);

        Assert.False(token.Valid);
    }

    [Fact]
    public void Valid_DefaultUserId_ReturnsFalse()
    {
        var token = NewTokenable(userId: default(Guid));

        Assert.False(token.Valid);
    }

    [Fact]
    public void Valid_DefaultDeviceId_ReturnsFalse()
    {
        var token = NewTokenable(deviceId: default(Guid));

        Assert.False(token.Valid);
    }

    [Theory, AutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(string identifier)
    {
        var token = NewTokenable();
        token.Identifier = identifier;

        Assert.False(token.Valid);
    }
}
