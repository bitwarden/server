using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Scim.Models;

public class ScimUserRequestModel : BaseScimUserModel
{
    public ScimUserRequestModel()
        : base(false) { }

    public OrganizationUserInvite ToOrganizationUserInvite(ScimProviderType scimProvider)
    {
        return new OrganizationUserInvite
        {
            Emails = new[] { EmailForInvite(scimProvider) },

            // Permissions cannot be set via SCIM so we use default values
            Type = OrganizationUserType.User,
            Collections = new List<CollectionAccessSelection>(),
            Groups = new List<Guid>(),
        };
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
