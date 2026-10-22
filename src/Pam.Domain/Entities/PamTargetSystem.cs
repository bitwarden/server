using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A system PAM rotates credentials on, through a <see cref="PamAccessConnector"/> or by a human out of band.
/// </summary>
public class PamTargetSystem : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    public PamTargetSystemMethod Method { get; set; }

    /// <summary>
    /// Sealed by the admin client under the org key; the server never reads it. Null while PendingSeal.
    /// </summary>
    [MaxLength(10000)]
    public string? SealedData { get; set; }

    /// <summary>
    /// The content key of SealedData, wrapped by the org key. Stored apart from SealedData so org-key rotation only
    /// re-wraps the key. Null while PendingSeal and for rows that predate the column.
    /// </summary>
    [MaxLength(2000)]
    public string? WrappedKey { get; set; }

    public PamTargetSystemStatus Status { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
