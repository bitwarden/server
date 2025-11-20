// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class MemberAccessReportQuery(
    IOrganizationMemberBaseDetailRepository organizationMemberBaseDetailRepository,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IApplicationCacheService applicationCacheService,
    ILogger<MemberAccessReportQuery> logger) : IMemberAccessReportQuery
{
    public async Task<IEnumerable<MemberAccessReportDetail>> GetMemberAccessReportsAsync(
        MemberAccessReportRequest request)
    {
        logger.LogInformation(Constants.BypassFiltersEventId, "Starting MemberAccessReport generation for OrganizationId: {OrganizationId}", request.OrganizationId);

        var baseDetails =
            await organizationMemberBaseDetailRepository.GetOrganizationMemberBaseDetailsByOrganizationId(
                request.OrganizationId);

        logger.LogInformation(Constants.BypassFiltersEventId, "Retrieved {BaseDetailsCount} base details for OrganizationId: {OrganizationId}",
            baseDetails.Count(), request.OrganizationId);

        var orgUsers = baseDetails.Select(x => x.UserGuid.GetValueOrDefault()).Distinct();
        var orgUsersCount = orgUsers.Count();
        logger.LogInformation(Constants.BypassFiltersEventId, "Found {UniqueUsersCount} unique users for OrganizationId: {OrganizationId}",
            orgUsersCount, request.OrganizationId);

        var orgUsersTwoFactorEnabled = await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(orgUsers);
        logger.LogInformation(Constants.BypassFiltersEventId, "Retrieved two-factor status for {UsersCount} users for OrganizationId: {OrganizationId}",
            orgUsersTwoFactorEnabled.Count(), request.OrganizationId);

        var orgAbility = await applicationCacheService.GetOrganizationAbilityAsync(request.OrganizationId);
        logger.LogInformation(Constants.BypassFiltersEventId, "Retrieved organization ability (UseResetPassword: {UseResetPassword}) for OrganizationId: {OrganizationId}",
            orgAbility?.UseResetPassword, request.OrganizationId);

        var accessDetails = baseDetails
            .GroupBy(b => new
            {
                b.UserGuid,
                b.UserName,
                b.Email,
                b.AvatarColor,
                b.TwoFactorProviders,
                b.ResetPasswordKey,
                b.UsesKeyConnector,
                b.GroupId,
                b.GroupName,
                b.CollectionId,
                b.CollectionName,
                b.ReadOnly,
                b.HidePasswords,
                b.Manage
            })
            .Select(g => new MemberAccessReportDetail
            {
                UserGuid = g.Key.UserGuid,
                UserName = g.Key.UserName,
                Email = g.Key.Email,
                AvatarColor = g.Key.AvatarColor,
                TwoFactorEnabled = orgUsersTwoFactorEnabled.FirstOrDefault(x => x.userId == g.Key.UserGuid).twoFactorIsEnabled,
                AccountRecoveryEnabled = !string.IsNullOrWhiteSpace(g.Key.ResetPasswordKey) && orgAbility.UseResetPassword,
                UsesKeyConnector = g.Key.UsesKeyConnector,
                GroupId = g.Key.GroupId,
                GroupName = g.Key.GroupName,
                CollectionId = g.Key.CollectionId,
                CollectionName = g.Key.CollectionName,
                ReadOnly = g.Key.ReadOnly,
                HidePasswords = g.Key.HidePasswords,
                Manage = g.Key.Manage,
                CipherIds = g.Select(c => c.CipherId).Where(id => id.HasValue).Select(id => id.Value)
            });

        var accessDetailsCount = accessDetails.Count();
        logger.LogInformation(Constants.BypassFiltersEventId, "Completed MemberAccessReport generation for OrganizationId: {OrganizationId}. Generated {AccessDetailsCount} access detail records",
            request.OrganizationId, accessDetailsCount);

        return accessDetails;
    }
}
