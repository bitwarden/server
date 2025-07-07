// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Models.Business;

public class OrganizationUserInvite
{
    public IEnumerable<string> Emails { get; set; }
    public Enums.OrganizationUserType? Type { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<CollectionAccessSelection> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }

    public OrganizationUserInvite() { }

    public OrganizationUserInvite(OrganizationUserInviteData requestModel)
    {
        Emails = requestModel.Emails;
        Type = requestModel.Type;
        AccessSecretsManager = requestModel.AccessSecretsManager;
        Collections = requestModel.Collections;
        Groups = requestModel.Groups;
        Permissions = requestModel.Permissions;
    }
}
