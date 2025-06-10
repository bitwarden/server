﻿using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class MemberAccessReportQuery(
    IOrganizationMemberBaseDetailRepository organizationMemberBaseDetailRepository,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IApplicationCacheService applicationCacheService)
    : IMemberAccessReportQuery
{
    public async Task<IEnumerable<MemberAccessReportDetail>> GetMemberAccessReportsAsync(
        MemberAccessReportRequest request)
    {
        var baseDetails =
            await organizationMemberBaseDetailRepository.GetOrganizationMemberBaseDetailsByOrganizationId(
                request.OrganizationId);

        var orgUsers = baseDetails.Select(x => x.UserGuid.GetValueOrDefault()).Distinct();
        var orgUsersTwoFactorEnabled = await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(orgUsers);

        var orgAbility = await applicationCacheService.GetOrganizationAbilityAsync(request.OrganizationId);

        var accessDetails = baseDetails
            .GroupBy(b => new
            {
                b.UserGuid,
                b.UserName,
                b.Email,
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
                CipherIds = g.Select(c => c.CipherId)
            });

        return accessDetails;
    }
}
