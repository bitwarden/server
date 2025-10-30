using Bit.Core.Dirt.Entities;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportResponseModel
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ReportData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public string? SummaryData { get; set; }
    public string? ApplicationData { get; set; }
    public int PasswordCount { get; set; }
    public int PasswordAtRiskCount { get; set; }
    public int MemberCount { get; set; }
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
        SummaryData = organizationReport.SummaryData;
        ApplicationData = organizationReport.ApplicationData;
        PasswordCount = organizationReport.PasswordCount ?? 0;
        PasswordAtRiskCount = organizationReport.PasswordAtRiskCount ?? 0;
        MemberCount = organizationReport.MemberCount ?? 0;
        CreationDate = organizationReport.CreationDate;
        RevisionDate = organizationReport.RevisionDate;
    }
}
