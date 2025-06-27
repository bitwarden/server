namespace Bit.Core.SecretsManager.Models.Data;

#nullable enable

public class PeopleGrantees
{
    public required IEnumerable<UserGrantee> UserGrantees { get; set; }
    public required IEnumerable<GroupGrantee> GroupGrantees { get; set; }
}

public class UserGrantee
{
    public Guid OrganizationUserId { get; set; }
    public string? Name { get; set; }
    public required string Email { get; set; }
    public bool CurrentUser { get; set; }
}

public class GroupGrantee
{
    public Guid GroupId { get; set; }
    public required string Name { get; set; }
    public bool CurrentUserInGroup { get; set; }
}
