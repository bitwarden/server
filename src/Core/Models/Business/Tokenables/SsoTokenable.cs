using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables;

public class SsoTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "BWUserPrefix_";
    public const string DataProtectorPurpose = "SsoTokenDataProtector";
    public const string TokenIdentifier = "ssoToken";

    public Guid OrganizationId { get; set; }
    public string DomainHint { get; set; }
    public string Identifier { get; set; } = TokenIdentifier;

    [JsonConstructor]
    public SsoTokenable() { }

    public SsoTokenable(Organization organization, double tokenLifetimeInSeconds) : this()
    {
        OrganizationId = organization?.Id ?? default;
        DomainHint = organization?.Identifier;
        ExpirationDate = DateTime.UtcNow.AddSeconds(tokenLifetimeInSeconds);
    }

    public bool TokenIsValid(Organization organization)
    {
        if (OrganizationId == default || DomainHint == default || organization == null || !Valid)
        {
            return false;
        }

        return organization.Identifier.Equals(DomainHint, StringComparison.InvariantCultureIgnoreCase)
            && organization.Id.Equals(OrganizationId);
    }

    // Validates deserialized 
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier
        && OrganizationId != default
        && !string.IsNullOrWhiteSpace(DomainHint);
}
