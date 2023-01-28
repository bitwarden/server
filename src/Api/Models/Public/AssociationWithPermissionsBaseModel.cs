using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Public;

public abstract class AssociationWithPermissionsBaseModel
{
    /// <summary>
    /// The associated object's unique identifier.
    /// </summary>
    /// <example>bfbc8338-e329-4dc0-b0c9-317c2ebf1a09</example>
    [Required]
    public Guid? Id { get; set; }
    /// <summary>
    /// When true, the read only permission will not allow the user or group to make changes to items.
    /// </summary>
    [Required]
    public bool? ReadOnly { get; set; }
}
