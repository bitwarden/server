using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Models;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// A named group of organization members, used to grant <see cref="Collection"/> access
/// to multiple members at once via <see cref="CollectionGroup"/> associations.
/// Members are associated with groups via <see cref="GroupUser"/>.
/// </summary>
public class Group : ITableObject<Guid>, IExternal
{
    /// <summary>
    /// A unique identifier for the group.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The ID of the <see cref="Organization"/> that owns this group.
    /// </summary>
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// The name of the group. Unencrypted.
    /// </summary>
    [MaxLength(100)]
    public string Name { get; set; } = null!;
    /// <summary>
    /// An ID used to associate this group with a record in an external directory service.
    /// Used by Directory Connector and SCIM.
    /// </summary>
    [MaxLength(300)]
    public string? ExternalId { get; set; }
    /// <summary>
    /// The date the group was created.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the group was last updated.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
