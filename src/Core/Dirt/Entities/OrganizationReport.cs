#nullable enable

using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Entities;

public class OrganizationReport : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTime Date { get; set; }
    public string SummaryData { get; set; } = string.Empty;
    public string ReportData { get; set; } = string.Empty;
    public string ApplicationData { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public string ContentEncryptionKey { get; set; } = string.Empty;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
