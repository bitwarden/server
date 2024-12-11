using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Public.Models;

public abstract class PolicyBaseModel
{
    /// <summary>
    /// Determines if this policy is enabled and enforced.
    /// </summary>
    [Required]
    public bool? Enabled { get; set; }

    /// <summary>
    /// Data for the policy.
    /// </summary>
    public Dictionary<string, object> Data { get; set; }
}
