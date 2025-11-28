// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserInviteData
{
    public IEnumerable<string> Emails { get; set; }
    public OrganizationUserType? Type { get; set; }
    public bool AccessSecretsManager { get; set; }
    public IEnumerable<CollectionAccessSelection> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }
    public Permissions Permissions { get; set; }
}
