using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Reports.Entities;

public class RiskInsightReport : ITableObject<Guid>, IRevisable
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTime Date { get; set; }
    public string ReportData { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
