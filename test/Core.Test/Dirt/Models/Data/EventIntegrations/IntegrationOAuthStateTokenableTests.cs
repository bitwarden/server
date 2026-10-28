using System.Security.Cryptography;
using System.Text;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Models.Data.EventIntegrations;

public class IntegrationOAuthStateTokenableTests
{
    private readonly IDataProtectorTokenFactory<IntegrationOAuthStateTokenable> _tokenFactory = CreateTokenFactory();

    [Theory, BitAutoData]
    public void Constructor_SetsPropertiesFromIntegration(OrganizationIntegration integration)
    {
        var timeProvider = new FakeTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var token = new IntegrationOAuthStateTokenable(integration, timeProvider);

        Assert.Equal(integration.Id, token.IntegrationId);
        Assert.Equal(integration.OrganizationId, token.OrganizationId);
        Assert.Equal(integration.Type, token.Type);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 20, 0, DateTimeKind.Utc), token.ExpirationDate);
    }

    [Theory, BitAutoData]
    public void ProtectUnprotect_RoundTripsCorrectly(OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        var token = new IntegrationOAuthStateTokenable(integration, TimeProvider.System);

        var state = _tokenFactory.Protect(token);

        Assert.StartsWith(IntegrationOAuthStateTokenable.ClearTextPrefix, state);
        Assert.True(_tokenFactory.TryUnprotect(state, out var parsed));
        Assert.True(parsed.Valid);
        Assert.True(parsed.IsValidFor(integration));
        Assert.Equal(integration.Id, parsed.IntegrationId);
        Assert.Equal(integration.OrganizationId, parsed.OrganizationId);
        Assert.Equal(IntegrationType.Slack, parsed.Type);
    }

    [Theory, BitAutoData]
    public void ProtectedState_DoesNotExposeIdentifiers(OrganizationIntegration integration)
    {
        var state = _tokenFactory.Protect(new IntegrationOAuthStateTokenable(integration, TimeProvider.System));

        Assert.DoesNotContain(integration.Id.ToString(), state, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(integration.OrganizationId.ToString(), state, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public void Unprotect_LegacyUnkeyedState_Fails(OrganizationIntegration integration)
    {
        // The previous state format could be computed by anyone who knew the organization and integration IDs
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{integration.OrganizationId:N}:{timestamp}")))[..12];
        var forgedState = $"{integration.Id}.{hash}.{timestamp}";

        Assert.False(_tokenFactory.TryUnprotect(forgedState, out _));
    }

    [Theory, BitAutoData]
    public void Unprotect_TamperedState_Fails(OrganizationIntegration integration)
    {
        var state = _tokenFactory.Protect(new IntegrationOAuthStateTokenable(integration, TimeProvider.System));

        // Flip a character in the protected payload, after the clear text prefix
        var index = state.Length - 10;
        var tampered = state[..index] + (state[index] == 'A' ? 'B' : 'A') + state[(index + 1)..];

        Assert.False(_tokenFactory.TryUnprotect(tampered, out _));
    }

    [Theory, BitAutoData]
    public void Unprotect_StateFromDifferentKey_Fails(OrganizationIntegration integration)
    {
        var state = CreateTokenFactory().Protect(new IntegrationOAuthStateTokenable(integration, TimeProvider.System));

        Assert.False(_tokenFactory.TryUnprotect(state, out _));
    }

    [Theory, BitAutoData]
    public void Valid_Expired_ReturnsFalse(OrganizationIntegration integration)
    {
        var token = new IntegrationOAuthStateTokenable(integration, TimeProvider.System)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        };

        var parsed = _tokenFactory.Unprotect(_tokenFactory.Protect(token));

        Assert.False(parsed.Valid);
        Assert.False(parsed.IsValidFor(integration));
    }

    [Theory, BitAutoData]
    public void Valid_WrongIdentifier_ReturnsFalse(OrganizationIntegration integration)
    {
        var token = new IntegrationOAuthStateTokenable(integration, TimeProvider.System)
        {
            Identifier = "not-the-right-identifier"
        };

        Assert.False(token.Valid);
    }

    [Fact]
    public void Valid_DefaultIds_ReturnsFalse()
    {
        var token = new IntegrationOAuthStateTokenable { ExpirationDate = DateTime.UtcNow.AddMinutes(5) };

        Assert.False(token.Valid);
    }

    [Theory, BitAutoData]
    public void IsValidFor_DifferentIntegrationId_ReturnsFalse(
        OrganizationIntegration integration,
        Guid otherIntegrationId)
    {
        var token = new IntegrationOAuthStateTokenable(integration, TimeProvider.System);
        integration.Id = otherIntegrationId;

        Assert.False(token.IsValidFor(integration));
    }

    [Theory, BitAutoData]
    public void IsValidFor_DifferentOrganization_ReturnsFalse(
        OrganizationIntegration integration,
        Guid otherOrganizationId)
    {
        var token = new IntegrationOAuthStateTokenable(integration, TimeProvider.System);
        integration.OrganizationId = otherOrganizationId;

        Assert.False(token.IsValidFor(integration));
    }

    [Theory, BitAutoData]
    public void IsValidFor_DifferentType_ReturnsFalse(OrganizationIntegration integration)
    {
        integration.Type = IntegrationType.Slack;
        var token = new IntegrationOAuthStateTokenable(integration, TimeProvider.System);
        integration.Type = IntegrationType.Teams;

        Assert.False(token.IsValidFor(integration));
    }

    private static IDataProtectorTokenFactory<IntegrationOAuthStateTokenable> CreateTokenFactory() =>
        new DataProtectorTokenFactory<IntegrationOAuthStateTokenable>(
            IntegrationOAuthStateTokenable.ClearTextPrefix,
            IntegrationOAuthStateTokenable.DataProtectorPurpose,
            new EphemeralDataProtectionProvider(),
            Substitute.For<ILogger<DataProtectorTokenFactory<IntegrationOAuthStateTokenable>>>());
}
