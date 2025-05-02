using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class OrganizationUserUserDetailsQuery : IOrganizationUserUserDetailsQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IFeatureService _featureService;
    private readonly IGetOrganizationUsersClaimedStatusQuery _getOrganizationUsersClaimedStatusQuery;

    public OrganizationUserUserDetailsQuery(
        IOrganizationUserRepository organizationUserRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IFeatureService featureService,
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery
    )
    {
        _organizationUserRepository = organizationUserRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _featureService = featureService;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
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

                o.Permissions = CoreHelpers.ClassToJsonData(userPermissions);

                return o;
            });
    }

    /// <summary>
    /// Get the organization user user details, two factor enabled status, and
    /// claimed status for the provided request.
    /// </summary>
    /// <param name="request">Request details for the query</param>
    /// <returns>List of OrganizationUserUserDetails</returns>
    public async Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> Get(OrganizationUserUserDetailsQueryRequest request)
    {
        var organizationUsers = await GetOrganizationUserUserDetails(request);

        var organizationUsersTwoFactorEnabled = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers)).ToDictionary(u => u.user.Id);
        var organizationUsersClaimedStatus = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));
        var responses = organizationUsers.Select(o => (o, organizationUsersTwoFactorEnabled[o.Id].twoFactorIsEnabled, organizationUsersClaimedStatus[o.Id]));


        return responses;
    }

    /// <summary>
    /// Get the organization users user details, two factor enabled status, and
    /// claimed status for confirmed users that are enrolled in account recovery
    /// </summary>
    /// <param name="request">Request details for the query</param>
    /// <returns>List of OrganizationUserUserDetails</returns>
    public async Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> GetAccountRecoveryEnrolledUsers(OrganizationUserUserDetailsQueryRequest request)
    {
        var organizationUsers = (await GetOrganizationUserUserDetails(request))
            .Where(o => o.Status.Equals(OrganizationUserStatusType.Confirmed) && o.UsesKeyConnector == false && !String.IsNullOrEmpty(o.ResetPasswordKey));

        var organizationUsersTwoFactorEnabled = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers)).ToDictionary(u => u.user.Id);
        var organizationUsersClaimedStatus = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));
        var responses = organizationUsers
            .Select(o => (o, organizationUsersTwoFactorEnabled[o.Id].twoFactorIsEnabled, organizationUsersClaimedStatus[o.Id]));

        return responses;
    }

}
