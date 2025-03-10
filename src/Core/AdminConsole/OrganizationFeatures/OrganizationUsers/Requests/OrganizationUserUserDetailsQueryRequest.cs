namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public class OrganizationUserUserDetailsQueryRequest
{
    public Guid OrganizationId { get; set; }
    public bool IncludeGroups { get; set; } = false;
    public bool IncludeCollections { get; set; } = false;
}
