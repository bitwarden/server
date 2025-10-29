using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.Models.Data;

public class OrganizationReportMetricsData
{
    public Guid OrganizationId { get; set; }
    public int? ApplicationCount { get; set; } = null;
    public int? ApplicationAtRiskCount { get; set; } = null;
    public int? CriticalApplicationCount { get; set; } = null;
    public int? CriticalApplicationAtRiskCount { get; set; } = null;
    public int? MemberCount { get; set; } = null;
    public int? MemberAtRiskCount { get; set; } = null;
    public int? CriticalMemberCount { get; set; } = null;
    public int? CriticalMemberAtRiskCount { get; set; } = null;
    public int? PasswordCount { get; set; } = null;
    public int? PasswordAtRiskCount { get; set; } = null;
    public int? CriticalPasswordCount { get; set; } = null;
    public int? CriticalPasswordAtRiskCount { get; set; } = null;

    public static OrganizationReportMetricsData from(Guid organizationId, OrganizationReportMetricsRequest request)
    {
        return new OrganizationReportMetricsData
        {
            OrganizationId = organizationId,
            ApplicationCount = request.ApplicationCount,
            ApplicationAtRiskCount = request.ApplicationAtRiskCount,
            CriticalApplicationCount = request.CriticalApplicationCount,
            CriticalApplicationAtRiskCount = request.CriticalApplicationAtRiskCount,
            MemberCount = request.MemberCount,
            MemberAtRiskCount = request.MemberAtRiskCount,
            CriticalMemberCount = request.CriticalMemberCount,
            CriticalMemberAtRiskCount = request.CriticalMemberAtRiskCount,
            PasswordCount = request.PasswordCount,
            PasswordAtRiskCount = request.PasswordAtRiskCount,
            CriticalPasswordCount = request.CriticalPasswordCount,
            CriticalPasswordAtRiskCount = request.CriticalPasswordAtRiskCount
        };
    }
}
