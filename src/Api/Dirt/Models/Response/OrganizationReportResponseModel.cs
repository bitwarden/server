using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportResponseModel
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ReportData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public string? SummaryData { get; set; }
    public string? ApplicationData { get; set; }
    public ReportFile? ReportFile { get; set; }
    public string? ReportFileDownloadUrl { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? RevisionDate { get; set; }

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
        ReportFile = organizationReport.GetReportFile();
        CreationDate = organizationReport.CreationDate;
        RevisionDate = organizationReport.RevisionDate;
    }
}
