using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to extend an active lease, identified by the route's lease id. The lease's end is pushed out by
/// <see cref="DurationSeconds"/>; a justifying <see cref="Reason"/> is required. Extensions are always auto-approved,
/// subject to the governing rule allowing extensions and the per-lease maximum not being reached.
/// </summary>
public class AccessLeaseExtensionRequestModel
{
    /// <summary>
    /// How far the lease's end is pushed out, in seconds. Must be positive and no longer than the governing rule's
    /// maximum extension duration.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DurationSeconds { get; set; }

    /// <summary>
    /// The justification recorded with the extension. Required to be non-empty.
    /// </summary>
    [Required]
    public string? Reason { get; set; }
}
