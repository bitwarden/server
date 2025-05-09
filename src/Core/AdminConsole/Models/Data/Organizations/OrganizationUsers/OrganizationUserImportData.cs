namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserImportData
{
    public HashSet<string> NewUsersSet { get; set; }
    public ICollection<OrganizationUserUserDetails> ExistingUsers { get; set; }
    public IEnumerable<OrganizationUserUserDetails> ExistingExternalUsers { get; set; }
    public Dictionary<string, Guid> ExistingExternalUsersIdDict { get; set; }

}
