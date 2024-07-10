using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Api.AdminConsole.Queries;

// Request object. If we need to do the Auth check in the query (see below). 
// The ClaimsPrincipal would need to be passed in from the endpoint. I see the
// AuthorizeAsync sort of does the same thing with the ClaimsPrincipal for passing
// to the service so it might not be a big deal
public class OrganizationUserUserDetailsQueryRequest
{
    // Again if this is necessary. Wanted to name it something so 
    // it's clear it is not the Bitwarden user info. Rather the ClaimsPrincipal
    // public ClaimsPrincipal RequestingUserContext { get; set; }
    public Guid OrganizationId { get; set; }
    public bool IncludeGroups { get; set; } = false;
    public bool IncludeCollections { get; set; } = false;
}

public class OrganizationUserUserDetailsQueryResponse
{
    public OrganizationUserUserDetails OrganizationUserUserDetails { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public Permissions Permissions { get; set; }

    public OrganizationUserUserDetailsQueryResponse(
        OrganizationUserUserDetails organizationUserUserDetails,
        bool twoFactorEnabled
    )
    {
        this.OrganizationUserUserDetails = organizationUserUserDetails;
        this.TwoFactorEnabled = twoFactorEnabled;
        // Convert the OrganizationUserUserDetails permissions json string 
        this.Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(OrganizationUserUserDetails.Permissions);
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

    public async Task<IEnumerable<OrganizationUserUserDetailsQueryResponse>> GetOrganizationUserUserDetails(OrganizationUserUserDetailsQueryRequest request)
    {
        // Should the query authorize? Or should this be the responsibility of the controller? 
        // Maybe having the query authorize would ensure safety?
        // Code from the controller for auth:
        // var authorized = (await _authorizationService.AuthorizeAsync(
        //     User, OrganizationUserOperations.ReadAll(orgId))).Succeeded;
        // if (!authorized)
        // {
        //     throw new NotFoundException();
        // }

        var organizationUsers = await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync(request.OrganizationId, request.IncludeGroups, request.IncludeCollections);

        var responseTasks = organizationUsers
            .Select(async o =>
            {
                var orgUser = new OrganizationUserUserDetailsQueryResponse(o,
                    await _userService.TwoFactorIsEnabledAsync(o));

                // Using the new extension method
                orgUser.OrganizationUserUserDetails.Type.GetFlexibleCollectionsUserType(orgUser.Permissions);

                // Set 'Edit/Delete Assigned Collections' custom permissions to false
                if (orgUser.Permissions is not null)
                {
                    orgUser.Permissions.EditAssignedCollections = false;
                    orgUser.Permissions.DeleteAssignedCollections = false;
                }

                return orgUser;
            });
        var responses = await Task.WhenAll(responseTasks);
        return responses;
    }
}
