using System.Security.Cryptography;
using System.Text;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class IntegrationOAuthState
{
    private const int _orgHashLength = 12;
    private static readonly TimeSpan _maxAge = TimeSpan.FromMinutes(20);

    public Guid IntegrationId { get; }
    private DateTimeOffset Issued { get; }
    private string OrganizationIdHash { get; }

    private IntegrationOAuthState(Guid integrationId, string organizationIdHash, DateTimeOffset issued)
    {
        IntegrationId = integrationId;
        OrganizationIdHash = organizationIdHash;
        Issued = issued;
    }

    public static IntegrationOAuthState FromIntegration(OrganizationIntegration integration, TimeProvider timeProvider)
    {
        var integrationId = integration.Id;
        var organizationIdHash = ComputeOrgHash(integration.OrganizationId);
        var issuedUtc = timeProvider.GetUtcNow();

        return new IntegrationOAuthState(integrationId, organizationIdHash, issuedUtc);
    }

    public static IntegrationOAuthState? FromString(string state, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;

        var parts = state.Split('.');
        if (parts.Length != 3) return null;

        // Verify timestamp
        if (!long.TryParse(parts[2], out var unixSeconds)) return null;

        var issuedUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var now = timeProvider.GetUtcNow();
        var age = now - issuedUtc;

        if (age > _maxAge) return null;

        // Parse integration id and store org
        if (!Guid.TryParse(parts[0], out var integrationId)) return null;
        var organizationIdHash = parts[1];

        return new IntegrationOAuthState(integrationId, organizationIdHash, issuedUtc);
    }

    public bool ValidateOrg(Guid orgId)
    {
        var expected = ComputeOrgHash(orgId);
        return expected == OrganizationIdHash;
    }

    public override string ToString()
    {
        return $"{IntegrationId}.{OrganizationIdHash}.{Issued.ToUnixTimeSeconds()}";
    }

    private static string ComputeOrgHash(Guid orgId)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(orgId.ToString("N")));
        return Convert.ToHexString(bytes)[.._orgHashLength];
    }
}
