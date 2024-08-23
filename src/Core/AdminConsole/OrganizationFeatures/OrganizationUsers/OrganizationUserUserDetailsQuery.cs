using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class OrganizationUserUserDetailsQuery : IOrganizationUserUserDetailsQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public OrganizationUserUserDetailsQuery(
        IOrganizationUserRepository organizationUserRepository
    )
    {
        _organizationUserRepository = organizationUserRepository;
    }

    /// <summary>
    /// Gets the organization user user details for the provided request
    /// </summary>
    /// <param name="request">Request details for the query</param>
    /// <returns>List of OrganizationUserUserDetails</returns>
    public async Task<IEnumerable<OrganizationUserUserDetails>> GetOrganizationUserUserDetails(OrganizationUserUserDetailsQueryRequest request)
    {
        var organizationUsers = await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync(request.OrganizationId, request.IncludeGroups, request.IncludeCollections);

        return organizationUsers
            .Select(o =>
            {
                var userPermissions = o.GetPermissions();

                // Downgrade Custom users with no other permissions than 'Edit/Delete Assigned Collections' to User
                o.Type = o.Type.GetFlexibleCollectionsUserType(userPermissions);

                return o;
            });
    }
}
