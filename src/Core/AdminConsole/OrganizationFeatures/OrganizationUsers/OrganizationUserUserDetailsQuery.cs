using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

namespace Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class OrganizationUserUserDetailsQuery : IOrganizationUserUserDetailsQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IGetOrganizationUsersClaimedStatusQuery _getOrganizationUsersClaimedStatusQuery;

    public OrganizationUserUserDetailsQuery(
        IOrganizationUserRepository organizationUserRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IGetOrganizationUsersClaimedStatusQuery getOrganizationUsersClaimedStatusQuery
    )
    {
        _organizationUserRepository = organizationUserRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _getOrganizationUsersClaimedStatusQuery = getOrganizationUsersClaimedStatusQuery;
    }

    /// <summary>
    /// Get the organization user user details, two factor enabled status, and
    /// claimed status for the provided request.
    /// </summary>
    /// <param name="request">Request details for the query</param>
    /// <returns>List of OrganizationUserUserDetails</returns>
    public async Task<IEnumerable<(OrganizationUserUserDetails OrgUser, bool TwoFactorEnabled, bool ClaimedByOrganization)>> Get(OrganizationUserUserDetailsQueryRequest request)
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
        var organizationUsers = (await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync_vNext(request.OrganizationId, request.IncludeGroups, request.IncludeCollections))
            .Where(o => o.Status.Equals(OrganizationUserStatusType.Confirmed) && o.UsesKeyConnector == false &&
                OrganizationUser.IsValidResetPasswordKey(o.ResetPasswordKey))
            .ToArray();

        var twoFactorTask = _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(organizationUsers);
        var claimedStatusTask = _getOrganizationUsersClaimedStatusQuery.GetUsersOrganizationClaimedStatusAsync(request.OrganizationId, organizationUsers.Select(o => o.Id));

        await Task.WhenAll(twoFactorTask, claimedStatusTask);

        var organizationUsersTwoFactorEnabled = twoFactorTask.Result.ToDictionary(u => u.user.Id, u => u.twoFactorIsEnabled);
        var organizationUsersClaimedStatus = claimedStatusTask.Result;
        var responses = organizationUsers.Select(organizationUserDetails =>
        {
            var userHasTwoFactorEnabled = organizationUsersTwoFactorEnabled[organizationUserDetails.Id];
            var userIsClaimedByOrganization = organizationUsersClaimedStatus[organizationUserDetails.Id];

            return (organizationUserDetails, userHasTwoFactorEnabled, userIsClaimedByOrganization);
        });

        return responses;
    }
}
