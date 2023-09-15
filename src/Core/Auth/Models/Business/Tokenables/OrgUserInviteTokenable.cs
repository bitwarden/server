using Bit.Core.Tokens;
using System.Text.Json.Serialization;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class OrgUserInviteTokenable : ExpiringTokenable
{
    // TODO: Ideally this would be internal and only visible to the test project.
    public static TimeSpan GetTokenLifetime() => TimeSpan.FromDays(5);

    public const string ClearTextPrefix = "BwOrgUserInviteToken_";

    // Backwards compatibility Note:
    // Must use existing DataProtectorPurpose to be able to decrypt tokens
    // in emailed invites that have not yet been accepted.
    public const string DataProtectorPurpose = "OrganizationServiceDataProtector";

    public const string TokenIdentifier = "OrgUserInviteToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid OrgUserId { get; set; }
    public string OrgUserEmail { get; set; }

    [JsonConstructor]
    public OrgUserInviteTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(GetTokenLifetime());
    }

    public OrgUserInviteTokenable(OrganizationUser orgUser) : this()
    {
        OrgUserId = orgUser?.Id ?? default;
        OrgUserEmail = orgUser?.Email;
    }

    public bool TokenIsValid(OrganizationUser orgUser)
    {
        if (OrgUserId == default || OrgUserEmail == default || orgUser == null)
        {
            return false;
        }

        return OrgUserId == orgUser.Id &&
               OrgUserEmail.Equals(orgUser.Email, StringComparison.InvariantCultureIgnoreCase);
    }

    // Validates deserialized
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && OrgUserId != default && !string.IsNullOrWhiteSpace(OrgUserEmail);
}
