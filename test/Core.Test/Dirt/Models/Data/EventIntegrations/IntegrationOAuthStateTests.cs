#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bit.Core.Test.Dirt.Models.Data.EventIntegrations;

public class IntegrationOAuthStateTests
{
    private readonly FakeTimeProvider _fakeTimeProvider = new(
        new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc)
    );

    [Theory, BitAutoData]
    public void FromIntegration_ToString_RoundTripsCorrectly(OrganizationIntegration integration)
    {
        var state = IntegrationOAuthState.FromIntegration(integration, _fakeTimeProvider);
        var parsed = IntegrationOAuthState.FromString(state.ToString(), _fakeTimeProvider);

        Assert.NotNull(parsed);
        Assert.Equal(state.IntegrationId, parsed.IntegrationId);
        Assert.True(parsed.ValidateOrg(integration.OrganizationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-state")]
    public void FromString_InvalidString_ReturnsNull(string state)
    {
        var parsed = IntegrationOAuthState.FromString(state, _fakeTimeProvider);

        Assert.Null(parsed);
    }

    [Fact]
    public void FromString_InvalidGuid_ReturnsNull()
    {
        var badState = $"not-a-guid.ABCD1234.1706313600";

        var parsed = IntegrationOAuthState.FromString(badState, _fakeTimeProvider);

        Assert.Null(parsed);
    }

    [Theory, BitAutoData]
    public void FromString_ExpiredState_ReturnsNull(OrganizationIntegration integration)
    {
        var state = IntegrationOAuthState.FromIntegration(integration, _fakeTimeProvider);

        // Advance time 30 minutes to exceed the 20-minute max age
        _fakeTimeProvider.Advance(TimeSpan.FromMinutes(30));

        var parsed = IntegrationOAuthState.FromString(state.ToString(), _fakeTimeProvider);

        Assert.Null(parsed);
    }

    [Theory, BitAutoData]
    public void ValidateOrg_WithCorrectOrgId_ReturnsTrue(OrganizationIntegration integration)
    {
        var state = IntegrationOAuthState.FromIntegration(integration, _fakeTimeProvider);

        Assert.True(state.ValidateOrg(integration.OrganizationId));
    }

    [Theory, BitAutoData]
    public void ValidateOrg_WithWrongOrgId_ReturnsFalse(OrganizationIntegration integration)
    {
        var state = IntegrationOAuthState.FromIntegration(integration, _fakeTimeProvider);

        Assert.False(state.ValidateOrg(Guid.NewGuid()));
    }

    [Theory, BitAutoData]
    public void ValidateOrg_ModifiedTimestamp_ReturnsFalse(OrganizationIntegration integration)
    {
        var state = IntegrationOAuthState.FromIntegration(integration, _fakeTimeProvider);
        var parts = state.ToString().Split('.');

        parts[2] = $"{_fakeTimeProvider.GetUtcNow().ToUnixTimeSeconds() - 1}";
        var modifiedState = IntegrationOAuthState.FromString(string.Join(".", parts), _fakeTimeProvider);

        Assert.True(state.ValidateOrg(integration.OrganizationId));
        Assert.NotNull(modifiedState);
        Assert.False(modifiedState.ValidateOrg(integration.OrganizationId));
    }
}
