using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// A partner's request for a file of all of its partnership's entitlements, produced in the background so the partner
/// can reconcile its records with Bitwarden's.
/// </summary>
public class OrganizationPartnershipReconciliationExport : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationPartnershipId { get; set; }
    public PartnershipReconciliationExportStatus Status { get; set; }
    /// <summary>
    /// The number of entitlements written. Set when the export completes.
    /// </summary>
    public int? RecordCount { get; set; }
    /// <summary>
    /// The export file's name in storage. Set only while a completed export's file exists.
    /// </summary>
    [MaxLength(256)]
    public string? BlobName { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletionDate { get; set; }
    /// <summary>
    /// When the export and its file are deleted. Advanced to the end of the retention period on completion.
    /// </summary>
    public DateTime ExpirationDate { get; set; }
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CombGuid.Generate();
        }
    }
}
