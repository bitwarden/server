using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Public.Models;

public abstract class GroupBaseModel
{
    /// <summary>
    /// The name of the group.
    /// </summary>
    /// <example>Development Team</example>
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    /// <summary>
    /// External identifier for reference or linking this group to another system, such as a user directory.
    /// </summary>
    /// <example>external_id_123456</example>
    [StringLength(300)]
    public string ExternalId { get; set; }
}
