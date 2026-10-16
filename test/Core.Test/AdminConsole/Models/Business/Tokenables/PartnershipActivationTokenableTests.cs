using System.Text.Json;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Business.Tokenables;

public class PartnershipActivationTokenableTests
{
    private static readonly Guid _partnershipId = Guid.NewGuid();
    private static readonly Guid _entitlementId = Guid.NewGuid();
    private const string ReturnUrl = "https://partner.example.com/done?ref=1";

    [Fact]
    public void ProtectUnprotect_PreservesData()
    {
        var factory = CreateFactory(new EphemeralDataProtectionProvider());
        var tokenable = new PartnershipActivationTokenable(_partnershipId, _entitlementId, ReturnUrl, DateTime.UtcNow);

        var recovered = factory.Unprotect(factory.Protect(tokenable));

        Assert.Equal(_partnershipId, recovered.OrganizationPartnershipId);
        Assert.Equal(_entitlementId, recovered.OrganizationPartnershipEntitlementId);
        Assert.Equal(tokenable.Nonce, recovered.Nonce);
        Assert.Equal(ReturnUrl, recovered.ReturnUrl);
        Assert.Equal(tokenable.ExpirationDate, recovered.ExpirationDate, TimeSpan.FromSeconds(1));
        Assert.True(recovered.Valid);
    }

    [Fact]
    public void Constructor_ExpiresFifteenMinutesAfterNow()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var tokenable = new PartnershipActivationTokenable(_partnershipId, _entitlementId, null, now);

        Assert.Equal(now.AddMinutes(15), tokenable.ExpirationDate);
    }

    [Fact]
    public void Valid_PastExpiration_IsFalse()
    {
        var tokenable = new PartnershipActivationTokenable(_partnershipId, _entitlementId, null,
            DateTime.UtcNow.AddMinutes(-16));

        Assert.True(tokenable.IsExpired);
        Assert.False(tokenable.Valid);
    }

    [Fact]
    public void Constructor_MintsDistinctNonces()
    {
        var first = new PartnershipActivationTokenable(_partnershipId, _entitlementId, null, DateTime.UtcNow);
        var second = new PartnershipActivationTokenable(_partnershipId, _entitlementId, null, DateTime.UtcNow);

        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    [Fact]
    public void Valid_DefaultInstance_IsFalse()
    {
        var tokenable = new PartnershipActivationTokenable { ExpirationDate = DateTime.UtcNow.AddMinutes(5) };

        Assert.False(tokenable.Valid);
    }

    /// <remarks>
    /// Guards against identity data such as a user ID, email, or external ID being added to the token. IsExpired and
    /// Valid are computed members the base tokenable serializes.
    /// </remarks>
    [Fact]
    public void Serialized_CarriesOnlyNonIdentifyingFields()
    {
        var tokenable = new PartnershipActivationTokenable(_partnershipId, _entitlementId, ReturnUrl, DateTime.UtcNow);

        var properties = JsonDocument.Parse(tokenable.ToToken().ToString()).RootElement
            .EnumerateObject().Select(p => p.Name).Order();

        Assert.Equal(
            ["ExpirationDate", "Identifier", "IsExpired", "Nonce", "OrganizationPartnershipEntitlementId",
                "OrganizationPartnershipId", "ReturnUrl", "Valid"],
            properties);
    }

    [Fact]
    public void Unprotect_TokenFromAnotherPurpose_Fails()
    {
        var provider = new EphemeralDataProtectionProvider();
        var otherFactory = new DataProtectorTokenFactory<PartnershipActivationTokenable>(
            PartnershipActivationTokenable.ClearTextPrefix, "OtherPurpose", provider,
            Substitute.For<ILogger<DataProtectorTokenFactory<PartnershipActivationTokenable>>>());
        var token = otherFactory.Protect(
            new PartnershipActivationTokenable(_partnershipId, _entitlementId, null, DateTime.UtcNow));

        Assert.False(CreateFactory(provider).TryUnprotect(token, out _));
    }

    private static DataProtectorTokenFactory<PartnershipActivationTokenable> CreateFactory(
        IDataProtectionProvider provider) =>
        new(PartnershipActivationTokenable.ClearTextPrefix, PartnershipActivationTokenable.DataProtectorPurpose,
            provider, Substitute.For<ILogger<DataProtectorTokenFactory<PartnershipActivationTokenable>>>());
}
