namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class OrganizationReportMetricsRequest
{
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
}
