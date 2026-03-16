using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class OrgUserInviteTokenable : ExpiringTokenable
{
    // TODO: PM-4317 - Ideally this would be internal and only visible to the test project.
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
    public string? OrgUserEmail { get; set; }

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

    public bool TokenIsValid(OrganizationUser? orgUser) => TokenIsValid(orgUser?.Id ?? default, orgUser?.Email);

    public bool TokenIsValid(Guid orgUserId, string? orgUserEmail)
    {
        if (OrgUserId == default || OrgUserEmail == default || orgUserId == default || orgUserEmail == default)
        {
            return false;
        }

        return OrgUserId == orgUserId &&
               OrgUserEmail.Equals(orgUserEmail, StringComparison.InvariantCultureIgnoreCase);
    }

    // Validates deserialized
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && OrgUserId != default && !string.IsNullOrWhiteSpace(OrgUserEmail);

    public static TokenableValidationErrors? ValidateOrgUserInvite(
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        string orgUserInviteToken,
        Guid orgUserId,
        string? orgUserEmail) =>
        orgUserInviteTokenDataFactory.TryUnprotect(orgUserInviteToken, out var decryptedToken) switch
        {
            true when decryptedToken.IsExpired => TokenableValidationErrors.ExpiringTokenables.Expired,
            true when !(decryptedToken.Valid && decryptedToken.TokenIsValid(orgUserId, orgUserEmail)) =>
                TokenableValidationErrors.InvalidToken,
            false => TokenableValidationErrors.InvalidToken,
            _ => null
        };

    public static bool ValidateOrgUserInviteStringToken(
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        string orgUserInviteToken, OrganizationUser orgUser) =>
        ValidateOrgUserInvite(orgUserInviteTokenDataFactory, orgUserInviteToken, orgUser.Id, orgUser.Email) is null;

    public static bool ValidateOrgUserInviteStringToken(
        IDataProtectorTokenFactory<OrgUserInviteTokenable> orgUserInviteTokenDataFactory,
        string orgUserInviteToken, Guid orgUserId, string orgUserEmail) =>
        ValidateOrgUserInvite(orgUserInviteTokenDataFactory, orgUserInviteToken, orgUserId, orgUserEmail) is null;
}
