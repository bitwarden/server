using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Request;

/// <summary>
/// A request to extend an active lease, identified by the route's lease id. The lease's end is pushed out by
/// <see cref="DurationSeconds"/>; a justifying <see cref="Reason"/> is required. Extensions are always auto-approved,
/// subject to the governing rule allowing extensions and the per-lease maximum not being reached.
/// </summary>
public class AccessLeaseExtensionRequestModel
{
    public int DurationSeconds { get; set; }

    public string? Reason { get; set; }

    public AccessLeaseExtensionSubmission ToSubmission(Guid leaseId) => new()
    {
        LeaseId = leaseId,
        DurationSeconds = DurationSeconds,
        Reason = Reason,
    };
}
