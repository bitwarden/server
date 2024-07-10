namespace Api.AdminConsole.Queries;

public interface IOrganizationUserUserDetailsQuery
{
    Task<IEnumerable<OrganizationUserUserDetailsQueryResponse>> GetOrganizationUserUserDetails(OrganizationUserUserDetailsQueryRequest request);
}
