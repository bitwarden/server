using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.PrivilegedAccessManagement.Entities;

/// <summary>
/// A reusable, org-scoped PAM leasing policy. Referenced by collections (and eventually Secrets Manager
/// entities) via FK to govern credential lease decisions.
/// </summary>
public class LeasingPolicy : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// JSON policy document. Validated by <c>LeasingPolicyValidator</c> before being persisted.
    /// </summary>
    public string Policy { get; set; } = null!;

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
