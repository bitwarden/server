using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Models.Business;

public class OrganizationUserInvite
{
    public IEnumerable<string> Emails { get; set; }
    public Enums.OrganizationUserType? Type { get; set; }
    public bool AccessAll { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<SelectionReadOnly> Collections { get; set; }

    public OrganizationUserInvite() { }

    public OrganizationUserInvite(OrganizationUserInviteData requestModel)
    {
        Emails = requestModel.Emails;
        Type = requestModel.Type;
        AccessAll = requestModel.AccessAll;
        Collections = requestModel.Collections;
        Permissions = requestModel.Permissions;
    }
}
