using System.ComponentModel.DataAnnotations;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to extend the routed lease. Extensions are approved automatically, once per lease, when the governing
/// rule allows them.
/// </summary>
public class AccessLeaseExtensionRequestModel
{
    /// <summary>
    /// How far the lease's end is pushed out, in seconds, bounded by the governing rule's maximum extension
    /// duration.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DurationSeconds { get; set; }

    /// <summary>The justification recorded with the extension.</summary>
    [Required]
    public string? Reason { get; set; }

    public AccessLeaseExtensionSubmission ToSubmission(Guid leaseId) => new()
    {
        LeaseId = leaseId,
        DurationSeconds = DurationSeconds,
        Reason = Reason,
    };
}
