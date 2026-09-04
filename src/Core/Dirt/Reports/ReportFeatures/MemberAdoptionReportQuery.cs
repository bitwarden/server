using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class MemberAdoptionReportQuery(
    IMemberAdoptionReportRepository memberAdoptionReportRepository,
    TimeProvider timeProvider,
    ILogger<MemberAdoptionReportQuery> logger) : IMemberAdoptionReportQuery
{
    private const int ActivityWindowDays = 30;

    public async Task<MemberAdoptionReportResult> GetMemberAdoptionReportAsync(MemberAdoptionReportRequest request)
    {
        var details = await memberAdoptionReportRepository
            .GetMemberAdoptionDetailsByOrganizationIdAsync(request.OrganizationId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activityCutoff = now.AddDays(-ActivityWindowDays);

        var totalMemberCount = 0;
        var activeMemberCount = 0;
        var sponsoredFamiliesRedeemedCount = 0;

        foreach (var detail in details)
        {
            totalMemberCount++;

            if (HasRecentLogin(detail, activityCutoff, now))
            {
                activeMemberCount++;
            }

            if (detail.HasRedeemedSponsorship)
            {
                sponsoredFamiliesRedeemedCount++;
            }
        }

        logger.LogInformation(Constants.BypassFiltersEventId,
            "Completed MemberAdoptionReport generation for OrganizationId: {OrganizationId}. {MemberCount} members, {ActiveMemberCount} active",
            request.OrganizationId, totalMemberCount, activeMemberCount);

        return new MemberAdoptionReportResult
        {
            TotalMemberCount = totalMemberCount,
            ActiveMemberCount = activeMemberCount,
            InactiveMemberCount = totalMemberCount - activeMemberCount,
            SponsoredFamiliesRedeemedCount = sponsoredFamiliesRedeemedCount,
            // Deferred: the response model projects straight to its own model as it writes, so the
            // members are never all live at once.
            Members = details.Select(detail => new MemberAdoptionReportMember
            {
                OrganizationUserId = detail.OrganizationUserId,
                UserId = detail.UserId,
                Name = detail.Name,
                Email = detail.Email,
                HasRecentLogin = HasRecentLogin(detail, activityCutoff, now),
                HasExtensionInstalled = detail.HasExtensionInstalled,
                VaultItemCount = detail.VaultItemCount,
                SharedItemCount = detail.SharedItemCount
            })
        };
    }

    private static bool HasRecentLogin(MemberAdoptionReportDetail detail, DateTime activityCutoff, DateTime now) =>
        detail.LastActivityDate is { } lastActivityDate
        && lastActivityDate >= activityCutoff
        && lastActivityDate <= now;
}
