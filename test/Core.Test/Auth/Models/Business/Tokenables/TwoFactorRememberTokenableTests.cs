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
        };

    /// <summary>
    /// A freshly constructed token must be valid. If <c>ExpirationDate</c> were left at its default,
    /// every token would be unusable the instant it was minted, which presents as a validation
    /// problem everywhere downstream rather than as a problem with this class.
    /// </summary>
    [Fact]
    public void Constructor_AfterInitialization_TokenIsValid()
    {
        var token = NewTokenable();

        Assert.False(token.IsExpired);
        Assert.True(token.Valid);
    }

    [Fact]
    public void Constructor_AfterInitialization_ExpirationSetToExpectedDuration()
    {
        var before = DateTime.UtcNow;
        var token = new TwoFactorRememberTokenable();
        var after = DateTime.UtcNow;

        Assert.InRange(
            token.ExpirationDate,
            before + TwoFactorRememberTokenable.GetTokenLifetime(),
            after + TwoFactorRememberTokenable.GetTokenLifetime());
    }

    /// <summary>
    /// Deserialization runs the constructor first and then overwrites <c>ExpirationDate</c> from the
    /// payload, which is what stops a round trip from granting a fresh lifetime.
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
