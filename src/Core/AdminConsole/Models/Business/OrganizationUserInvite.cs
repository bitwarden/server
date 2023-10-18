using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Business;

public class OrganizationUserInvite
{
    public IEnumerable<string> Emails { get; set; }
    public OrganizationUserType? Type { get; set; }
    public bool AccessAll { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<CollectionAccessSelection> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }

    public OrganizationUserInvite() { }

    public OrganizationUserInvite(OrganizationUserInviteData requestModel)
    {
        Emails = requestModel.Emails;
        Type = requestModel.Type;
        AccessAll = requestModel.AccessAll;
        AccessSecretsManager = requestModel.AccessSecretsManager;
        Collections = requestModel.Collections;
        Groups = requestModel.Groups;
        Permissions = requestModel.Permissions;
    }
}
