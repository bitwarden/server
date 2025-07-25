using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Entities;

public class OrganizationReportSummary : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationReportId { get; set; }
    public string SummaryDetails { get; set; } = string.Empty;
    public string ContentEncryptionKey { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdateDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
