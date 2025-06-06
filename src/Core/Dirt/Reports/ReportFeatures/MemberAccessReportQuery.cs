using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Tools.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class MemberAccessReportQuery : IMemberAccessReportQuery
{
    private readonly IOrganizationMemberBaseDetailRepository _organizationMemberBaseDetailRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly IApplicationCacheService _applicationCacheService;

    public MemberAccessReportQuery(
        IOrganizationMemberBaseDetailRepository organizationMemberBaseDetailRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        IApplicationCacheService applicationCacheService)
    {
        _organizationMemberBaseDetailRepository = organizationMemberBaseDetailRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _applicationCacheService = applicationCacheService;
    }

    public async Task<IEnumerable<MemberAccessReportDetail>> GetMemberAccessReportsAsync(
        MemberAccessReportRequest request)
    {

    }
}
