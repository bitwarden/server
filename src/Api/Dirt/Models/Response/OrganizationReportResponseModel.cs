using Bit.Core.Dirt.Entities;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportResponseModel
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReportData { get; set; } = string.Empty;
    public string ContentEncryptionKey { get; set; } = string.Empty;
    public string SummaryData { get; set; } = string.Empty;
    public string ApplicationData { get; set; } = string.Empty;
    public int PasswordCount { get; set; } = 0;
    public int PasswordAtRiskCount { get; set; } = 0;
    public int MemberCount { get; set; } = 0;
    public DateTime? CreationDate { get; set; } = null;
    public DateTime? RevisionDate { get; set; } = null;

    public OrganizationReportResponseModel(OrganizationReport organizationReport)
    {
        if (organizationReport == null)
        {
            return;
        }

        Id = organizationReport.Id;
        OrganizationId = organizationReport.OrganizationId;
        ReportData = organizationReport.ReportData;
        ContentEncryptionKey = organizationReport.ContentEncryptionKey;
        SummaryData = organizationReport.SummaryData ?? string.Empty;
        ApplicationData = organizationReport.ApplicationData ?? string.Empty;
        PasswordCount = organizationReport.PasswordCount ?? 0;
        PasswordAtRiskCount = organizationReport.PasswordAtRiskCount ?? 0;
        MemberCount = organizationReport.MemberCount ?? 0;
        CreationDate = organizationReport.CreationDate;
        RevisionDate = organizationReport.RevisionDate;
    }
}
