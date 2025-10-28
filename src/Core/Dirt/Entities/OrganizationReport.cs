#nullable enable

using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Entities;

public class OrganizationReport : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReportData { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string ContentEncryptionKey { get; set; } = string.Empty;
    public string? SummaryData { get; set; }
    public string? ApplicationData { get; set; }
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public int? ApplicationCount { get; set; }
    public int? ApplicationAtRiskCount { get; set; }
    public int? CriticalApplicationCount { get; set; }
    public int? CriticalApplicationAtRiskCount { get; set; }
    public int? MemberCount { get; set; }
    public int? MemberAtRiskCount { get; set; }
    public int? CriticalMemberCount { get; set; }
    public int? CriticalMemberAtRiskCount { get; set; }
    public int? PasswordCount { get; set; }
    public int? PasswordAtRiskCount { get; set; }
    public int? CriticalPasswordCount { get; set; }
    public int? CriticalPasswordAtRiskCount { get; set; }



    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
