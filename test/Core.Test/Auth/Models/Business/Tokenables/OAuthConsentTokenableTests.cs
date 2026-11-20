using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Business.Tokenables;

public class OAuthConsentTokenableTests
{
    private const string _returnUrl = "/connect/authorize?client_id=partner.acme&scope=openid";
    private const string _nonce = "test-nonce";

    private readonly IDataProtectorTokenFactory<OAuthConsentTokenable> _tokenFactory = CreateTokenFactory();

    [Theory, BitAutoData]
    public void Factory_SetsFieldsAndExpiry(Guid userId)
    {
        var timeProvider = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var factory = new OAuthConsentTokenableFactory(timeProvider);

        var token = factory.CreateToken(userId, _returnUrl, _nonce);

        Assert.Equal(userId, token.UserId);
        Assert.Equal(_nonce, token.Nonce);
        Assert.Equal(OAuthConsentTokenable.HashAuthorizationRequest(_returnUrl), token.AuthorizationRequestHash);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc), token.ExpirationDate);
        Assert.True(token.Valid);
        Assert.True(token.IsValidFor(userId, _returnUrl));
    }

    [Theory, BitAutoData]
    public void ProtectUnprotect_RoundTrips(Guid userId)
    {
        var token = new OAuthConsentTokenableFactory(TimeProvider.System).CreateToken(userId, _returnUrl, _nonce);

        var protectedToken = _tokenFactory.Protect(token);

        Assert.StartsWith(OAuthConsentTokenable.ClearTextPrefix, protectedToken);
        Assert.True(_tokenFactory.TryUnprotect(protectedToken, out var parsed));
        Assert.True(parsed.Valid);
        Assert.True(parsed.IsValidFor(userId, _returnUrl));
    }

    [Theory, BitAutoData]
    public void IsValidFor_DifferentUser_ReturnsFalse(Guid userId, Guid otherUserId)
    {
        var token = new OAuthConsentTokenableFactory(TimeProvider.System).CreateToken(userId, _returnUrl, _nonce);

        Assert.False(token.IsValidFor(otherUserId, _returnUrl));
    }

    [Theory, BitAutoData]
    public void IsValidFor_DifferentReturnUrl_ReturnsFalse(Guid userId)
    {
        var token = new OAuthConsentTokenableFactory(TimeProvider.System).CreateToken(userId, _returnUrl, _nonce);

        Assert.False(token.IsValidFor(userId, "/connect/authorize?client_id=partner.evil"));
    }

    [Theory, BitAutoData]
    public void Valid_Expired_ReturnsFalse(Guid userId)
    {
        var timeProvider = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var token = new OAuthConsentTokenableFactory(timeProvider).CreateToken(userId, _returnUrl, _nonce);

        timeProvider.Advance(OAuthConsentTokenable.Lifetime + TimeSpan.FromSeconds(1));

        Assert.False(token.Valid);
        Assert.False(token.IsValidFor(userId, _returnUrl));
    }

    [Fact]
    public void Valid_DefaultToken_ReturnsFalse()
    {
        var token = new OAuthConsentTokenable { ExpirationDate = DateTime.UtcNow.AddMinutes(5) };

        Assert.False(token.Valid);
    }

    private static IDataProtectorTokenFactory<OAuthConsentTokenable> CreateTokenFactory() =>
        new DataProtectorTokenFactory<OAuthConsentTokenable>(
            OAuthConsentTokenable.ClearTextPrefix,
            OAuthConsentTokenable.DataProtectorPurpose,
            new EphemeralDataProtectionProvider(),
            Substitute.For<ILogger<DataProtectorTokenFactory<OAuthConsentTokenable>>>());
}
