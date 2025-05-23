namespace Bit.Core.Tools.ReportFeatures.Requests;

public class RiskInsightsReportResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReportData { get; set; }
    public int TotalMembers { get; set; }
    public int TotalAtRiskMembers { get; set; }
    public int TotalApplications { get; set; }
    public int TotalAtRiskApplications { get; set; }
    public int TotalCriticalApplications { get; set; }
}
