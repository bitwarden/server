using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>The body of <c>POST daemons/{id}/assignments</c>: the target system to assign the daemon to.</summary>
public class AssignDaemonTargetRequestModel
{
    /// <summary>
    /// The target system the daemon should work. Assignment is what makes the target's rotation jobs visible to
    /// the daemon -- an unassigned daemon sees no work, and a manual target has no daemon to assign.
    /// </summary>
    [Required]
    public Guid TargetSystemId { get; set; }
}
