#nullable enable

namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportSummaryDataResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? SummaryData { get; set; }
}
