using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class OrgUserInviteTokenable : ExpiringTokenable
{
    // TODO: Ideally this would be internal and only visible to the test project.
    // but configuring that is out of scope for these changes.
    public static TimeSpan GetTokenLifetime() => TimeSpan.FromDays(5);

    public const string ClearTextPrefix = "BwOrgUserInviteToken_";

    // Backwards compatibility Note:
    // Previously, tokens were manually created in the OrganizationService using a data protector
    // initialized with purpose: "OrganizationServiceDataProtector"
    // So, we must continue to use the existing purpose to be able to decrypt tokens
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
