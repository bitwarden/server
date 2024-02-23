﻿using System.ComponentModel.DataAnnotations;

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
    /// Determines if this group can access all collections within the organization, or only the associated
    /// collections. If set to <c>true</c>, this option overrides any collection assignments. If your organization is using
    /// the latest collection enhancements, you will not be allowed to set this property to <c>true</c>.
    /// </summary>
    public bool? AccessAll { get; set; }
    /// <summary>
    /// External identifier for reference or linking this group to another system, such as a user directory.
    /// </summary>
    /// <example>external_id_123456</example>
    [StringLength(300)]
    public string ExternalId { get; set; }
}
