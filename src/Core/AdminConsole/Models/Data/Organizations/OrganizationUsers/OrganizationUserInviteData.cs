using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserInviteData
{
    public IEnumerable<string> Emails { get; set; }
    public OrganizationUserType? Type { get; set; }
    public bool AccessAll { get; set; }
    public bool AccessSecretsManager { get; set; }
    public IEnumerable<CollectionAccessSelection> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }
    public Permissions Permissions { get; set; }
}
