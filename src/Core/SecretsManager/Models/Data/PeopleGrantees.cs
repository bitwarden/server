// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.SecretsManager.Models.Data;

public class PeopleGrantees
{
    public IEnumerable<UserGrantee> UserGrantees { get; set; }
    public IEnumerable<GroupGrantee> GroupGrantees { get; set; }
}

public class UserGrantee
{
    public Guid OrganizationUserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool CurrentUser { get; set; }
}

public class GroupGrantee
{
    public Guid GroupId { get; set; }
    public string Name { get; set; }
    public bool CurrentUserInGroup { get; set; }
}
