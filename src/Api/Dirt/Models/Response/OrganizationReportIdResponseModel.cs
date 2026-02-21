using Bit.Core.Dirt.Entities;

namespace Bit.Api.Dirt.Models.Response;

/// <summary>
/// Lightweight response model for organization report operations that returns
/// only essential metadata without large data fields (ReportData, SummaryData, ApplicationData).
/// Used to avoid JSON serialization limits when handling large reports.
/// </summary>
public class OrganizationReportIdResponseModel
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? RevisionDate { get; set; }

    public OrganizationReportIdResponseModel(OrganizationReport organizationReport)
    {
        if (organizationReport == null)
        {
            return;
        }

        Id = organizationReport.Id;
        OrganizationId = organizationReport.OrganizationId;
        CreationDate = organizationReport.CreationDate;
        RevisionDate = organizationReport.RevisionDate;
    }
}
