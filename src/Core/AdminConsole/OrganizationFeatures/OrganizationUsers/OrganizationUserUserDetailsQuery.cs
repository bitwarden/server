using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Responses;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class OrganizationUserUserDetailsQuery : IOrganizationUserUserDetailsQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;

    public OrganizationUserUserDetailsQuery(
        IOrganizationUserRepository organizationUserRepository,
        IUserService userService
    )
    {
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
    }

    /// <summary>
    /// Gets the organization user user details for the provided request
    /// </summary>
    /// <param name="request">Request details for the query</param>
    /// <returns>List of OrganizationUserUserDetailsQueryResponse</returns>
    public async Task<IEnumerable<OrganizationUserUserDetailsQueryResponse>> GetOrganizationUserUserDetails(OrganizationUserUserDetailsQueryRequest request)
    {
        var organizationUsers = await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync(request.OrganizationId, request.IncludeGroups, request.IncludeCollections);

        var responseTasks = organizationUsers
            .Select(async o =>
            {
                var orgUser = new OrganizationUserUserDetailsQueryResponse(o,
                    await _userService.TwoFactorIsEnabledAsync(o));

                var userPermissions = o.GetPermissions();
                orgUser.OrganizationUserUserDetails.Type = orgUser.OrganizationUserUserDetails.Type.GetFlexibleCollectionsUserType(userPermissions);

                // Set 'Edit/Delete Assigned Collections' custom permissions to false
                if (userPermissions is not null)
                {
                    userPermissions.EditAssignedCollections = false;
                    userPermissions.DeleteAssignedCollections = false;
                }

                return orgUser;
            });
        var responses = await Task.WhenAll(responseTasks);
        return responses;
    }
}