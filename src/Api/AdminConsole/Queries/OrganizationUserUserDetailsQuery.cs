using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Api.AdminConsole.Queries;


public class OrganizationUserUserDetailsQueryRequest
{
    public Guid OrganizationId { get; set; }
    public bool IncludeGroups { get; set; } = false;
    public bool IncludeCollections { get; set; } = false;
}

public class OrganizationUserUserDetailsQueryResponse
{
    public OrganizationUserUserDetails OrganizationUserUserDetails { get; set; }
    public bool TwoFactorEnabled { get; set; }

    public OrganizationUserUserDetailsQueryResponse(
        OrganizationUserUserDetails organizationUserUserDetails,
        bool twoFactorEnabled
    )
    {
        this.OrganizationUserUserDetails = organizationUserUserDetails;
        this.TwoFactorEnabled = twoFactorEnabled;
    }
}

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
