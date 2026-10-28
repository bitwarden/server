using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

/// <summary>
/// OAuth <c>state</c> for the Slack and Teams integration flows. Protected with ASP.NET Data Protection so it
/// can only be minted by the server (after an Organization Owner check) and cannot be forged or tampered with.
/// </summary>
public class IntegrationOAuthStateTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "BwIntegrationOAuthState";
    public const string DataProtectorPurpose = "IntegrationOAuthStateDataProtector";
    public const string TokenIdentifier = "IntegrationOAuthStateToken";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid IntegrationId { get; set; }
    public Guid OrganizationId { get; set; }
    public IntegrationType Type { get; set; }

    public IntegrationOAuthStateTokenable() { }

    public IntegrationOAuthStateTokenable(OrganizationIntegration integration, TimeProvider timeProvider)
    {
        IntegrationId = integration.Id;
        OrganizationId = integration.OrganizationId;
        Type = integration.Type;
        ExpirationDate = timeProvider.GetUtcNow().UtcDateTime.Add(Lifetime);
    }

    /// <summary>
    /// Checks that the token is unexpired and was issued for exactly this integration.
    /// </summary>
    public bool IsValidFor(OrganizationIntegration integration) =>
        Valid &&
        IntegrationId == integration.Id &&
        OrganizationId == integration.OrganizationId &&
        Type == integration.Type;

    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier &&
        IntegrationId != default &&
        OrganizationId != default;
}
