namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserImportData
{
    public HashSet<string> NewUsersSet { get; init; }
    public ICollection<OrganizationUserUserDetails> ExistingUsers { get; init; }
    public IEnumerable<OrganizationUserUserDetails> ExistingExternalUsers { get; init; }
    public Dictionary<string, Guid> ExistingExternalUsersIdDict { get; init; }

    public OrganizationUserImportData(ICollection<OrganizationUserUserDetails> existingUsers, HashSet<string> newUsersSet)
    {
        NewUsersSet = newUsersSet;
        ExistingUsers = existingUsers;
        ExistingExternalUsers = GetExistingExternalUsers(existingUsers);
        ExistingExternalUsersIdDict = GetExistingExternalUsers(existingUsers).ToDictionary(u => u.ExternalId, u => u.Id);
    }

    private IEnumerable<OrganizationUserUserDetails> GetExistingExternalUsers(ICollection<OrganizationUserUserDetails> existingUsers)
    {
        return existingUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId))
            .ToList();
    }
}
