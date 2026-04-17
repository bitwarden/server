using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.Models.Data;

public class OrganizationReportMetricsData
{
    public Guid OrganizationId { get; set; }
    public int? ApplicationCount { get; set; }
    public int? ApplicationAtRiskCount { get; set; }
    public int? CriticalApplicationCount { get; set; }
    public int? CriticalApplicationAtRiskCount { get; set; }
    public int? MemberCount { get; set; }
    public int? MemberAtRiskCount { get; set; }
    public int? CriticalMemberCount { get; set; }
    public int? CriticalMemberAtRiskCount { get; set; }
    public int? PasswordCount { get; set; }
    public int? PasswordAtRiskCount { get; set; }
    public int? CriticalPasswordCount { get; set; }
    public int? CriticalPasswordAtRiskCount { get; set; }

    public static OrganizationReportMetricsData From(Guid organizationId, OrganizationReportMetricsRequest? request)
    {
        if (request == null)
        {
            return new OrganizationReportMetricsData
            {
                OrganizationId = organizationId
            };
        }

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
