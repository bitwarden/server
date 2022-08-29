using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserInviteData
{
    public IEnumerable<string> Emails { get; set; }
    public OrganizationUserType? Type { get; set; }
    public bool AccessAll { get; set; }
    public IEnumerable<SelectionReadOnly> Collections { get; set; }
    public Permissions Permissions { get; set; }
}
