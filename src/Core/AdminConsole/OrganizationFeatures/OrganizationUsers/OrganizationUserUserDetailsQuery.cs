using Bit.Core;
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
                // Only set permissions for Custom user types for performance optimization
                if (o.Type == OrganizationUserType.Custom)
                {
                    var userPermissions = o.GetPermissions();
                    o.Permissions = CoreHelpers.ClassToJsonData(userPermissions);
                }

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
        if (_featureService.IsEnabled(FeatureFlagKeys.MembersGetEndpointOptimization))
        {
            return await Get_vNext(request);
        }

        var organizationUsers = await GetOrganizationUserUserDetails(request);

        var organizationUsersTwoFactorEnabled = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers)).ToDictionary(u => u.user.Id);
        var organizationUsersClaimedStatus = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));
        var responses = organizationUsers.Select(o => (o, organizationUsersTwoFactorEnabled[o.Id].twoFactorIsEnabled, organizationUsersClaimedStatus[o.Id]));


        return responses;
    }

    private async Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> Get_vNext(OrganizationUserUserDetailsQueryRequest request)
    {
        var organizationUsers = await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync_vNext(request.OrganizationId, request.IncludeGroups, request.IncludeCollections);

        var twoFactorTask = _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers);
        var claimedStatusTask = _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));

        await Task.WhenAll(twoFactorTask, claimedStatusTask);

        var organizationUsersTwoFactorEnabled = twoFactorTask.Result.ToDictionary(u => u.user.Id, u => u.twoFactorIsEnabled);
        var organizationUsersClaimedStatus = claimedStatusTask.Result;
        var responses = organizationUsers.Select(organizationUserDetails =>
        {
            // Only set permissions for Custom user types for performance optimization
            if (organizationUserDetails.Type == OrganizationUserType.Custom)
            {
                var organizationUserPermissions = organizationUserDetails.GetPermissions();
                organizationUserDetails.Permissions = CoreHelpers.ClassToJsonData(organizationUserPermissions);
            }

            var userHasTwoFactorEnabled = organizationUsersTwoFactorEnabled[organizationUserDetails.Id];
            var userIsClaimedByOrganization = organizationUsersClaimedStatus[organizationUserDetails.Id];

            return (organizationUserDetails, userHasTwoFactorEnabled, userIsClaimedByOrganization);
        });

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
        if (_featureService.IsEnabled(FeatureFlagKeys.MembersGetEndpointOptimization))
        {
            return await GetAccountRecoveryEnrolledUsers_vNext(request);
        }

        var organizationUsers = (await GetOrganizationUserUserDetails(request))
            .Where(o => o.Status.Equals(OrganizationUserStatusType.Confirmed) && o.UsesKeyConnector == false && !String.IsNullOrEmpty(o.ResetPasswordKey));

        var organizationUsersTwoFactorEnabled = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers)).ToDictionary(u => u.user.Id);
        var organizationUsersClaimedStatus = await _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));
        var responses = organizationUsers
            .Select(o => (o, organizationUsersTwoFactorEnabled[o.Id].twoFactorIsEnabled, organizationUsersClaimedStatus[o.Id]));

        return responses;
    }

    private async Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> GetAccountRecoveryEnrolledUsers_vNext(OrganizationUserUserDetailsQueryRequest request)
    {
        var organizationUsers = (await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync_vNext(request.OrganizationId, request.IncludeGroups, request.IncludeCollections))
            .Where(o => o.Status.Equals(OrganizationUserStatusType.Confirmed) && o.UsesKeyConnector == false && !String.IsNullOrEmpty(o.ResetPasswordKey))
            .ToArray();

        var twoFactorTask = _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers);
        var claimedStatusTask = _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));

        await Task.WhenAll(twoFactorTask, claimedStatusTask);

        var organizationUsersTwoFactorEnabled = twoFactorTask.Result.ToDictionary(u => u.user.Id, u => u.twoFactorIsEnabled);
        var organizationUsersClaimedStatus = claimedStatusTask.Result;
        var responses = organizationUsers.Select(organizationUserDetails =>
        {
            // Only set permissions for Custom user types for performance optimization
            if (organizationUserDetails.Type == OrganizationUserType.Custom)
            {
                var organizationUserPermissions = organizationUserDetails.GetPermissions();
                organizationUserDetails.Permissions = CoreHelpers.ClassToJsonData(organizationUserPermissions);
            }

            var userHasTwoFactorEnabled = organizationUsersTwoFactorEnabled[organizationUserDetails.Id];
            var userIsClaimedByOrganization = organizationUsersClaimedStatus[organizationUserDetails.Id];

            return (organizationUserDetails, userHasTwoFactorEnabled, userIsClaimedByOrganization);
        });

        return responses;
    }
}
