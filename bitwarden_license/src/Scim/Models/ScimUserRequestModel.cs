// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using OrganizationUserInvite = Bit.Core.Models.Business.OrganizationUserInvite;

namespace Bit.Scim.Models;

public class ScimUserRequestModel : BaseScimUserModel
{
    public ScimUserRequestModel()
        : base(false)
    {
    }

    public OrganizationUserInvite ToOrganizationUserInvite(ScimProviderType scimProvider)
    {
        return new OrganizationUserInvite
        {
            Emails = new[] { EmailForInvite(scimProvider) },

            // Permissions cannot be set via SCIM so we use default values
            Type = OrganizationUserType.User,
            Collections = new List<CollectionAccessSelection>(),
            Groups = new List<Guid>()
        };
    }

    public InviteOrganizationUsersRequest ToRequest(
        ScimProviderType scimProvider,
        InviteOrganization inviteOrganization,
        DateTimeOffset performedAt)
    {
        var email = EmailForInvite(scimProvider);

        if (string.IsNullOrWhiteSpace(email) || !Active)
        {
            throw new BadRequestException();
        }

        return new InviteOrganizationUsersRequest(
            invites:
            [
                new Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.OrganizationUserInvite(
                        email: email,
                        externalId: ExternalIdForInvite()
                    )
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty, // SCIM does not have a user id
            performedAt: performedAt);
    }

    private string EmailForInvite(ScimProviderType scimProvider)
    {
        var email = PrimaryEmail?.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        switch (scimProvider)
        {
            case ScimProviderType.AzureAd:
                return UserName?.ToLowerInvariant();
            default:
                email = WorkEmail?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email))
                {
                    email = Emails?.FirstOrDefault()?.Value?.ToLowerInvariant();
                }

                return email;
        }
    }

    public string ExternalIdForInvite()
    {
        if (!string.IsNullOrWhiteSpace(ExternalId))
        {
            return ExternalId;
        }

        if (!string.IsNullOrWhiteSpace(UserName))
        {
            return UserName;
        }

        return CoreHelpers.RandomString(15);
    }
}
