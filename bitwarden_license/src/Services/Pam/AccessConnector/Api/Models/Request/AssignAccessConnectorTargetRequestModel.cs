using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Request;

/// <summary>The body of <c>POST access-connectors/{id}/assignments</c>: the target system to assign the access
/// connector to.</summary>
public class AssignAccessConnectorTargetRequestModel
{
    /// <summary>
    /// The target system the access connector should work. Assignment is what makes the target's rotation jobs visible
    /// to the access connector -- an unassigned access connector sees no work, and a manual target has no access
    /// connector to assign.
    /// </summary>
    [Required]
    public Guid TargetSystemId { get; set; }
}
