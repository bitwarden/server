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
        var details = (await memberAdoptionReportRepository
            .GetMemberAdoptionDetailsByOrganizationIdAsync(request.OrganizationId)).ToList();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activityCutoff = now.AddDays(-ActivityWindowDays);

        var members = details
            .Select(detail => new MemberAdoptionReportMember
            {
                OrganizationUserId = detail.OrganizationUserId,
                UserId = detail.UserId,
                Name = detail.Name,
                Email = detail.Email,
                HasRecentLogin = detail.LastActivityDate is { } lastActivityDate
                                 && lastActivityDate >= activityCutoff
                                 && lastActivityDate <= now,
                HasExtensionInstalled = detail.HasExtensionInstalled,
                VaultItemCount = detail.VaultItemCount,
                SharedItemCount = detail.SharedItemCount
            })
            .ToList();

        var activeMemberCount = members.Count(member => member.HasRecentLogin);

        logger.LogInformation(Constants.BypassFiltersEventId,
            "Completed MemberAdoptionReport generation for OrganizationId: {OrganizationId}. {MemberCount} members, {ActiveMemberCount} active",
            request.OrganizationId, members.Count, activeMemberCount);

        return new MemberAdoptionReportResult
        {
            TotalMemberCount = members.Count,
            ActiveMemberCount = activeMemberCount,
            InactiveMemberCount = members.Count - activeMemberCount,
            SponsoredFamiliesRedeemedCount = details.Count(detail => detail.HasRedeemedSponsorship),
            Members = members
        };
    }
}
