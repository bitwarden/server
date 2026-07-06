using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>The body of <c>POST daemons/{id}/assignments</c>: the target system to assign the daemon to.</summary>
public class AssignDaemonTargetRequestModel
{
    [Required]
    public Guid TargetSystemId { get; set; }
}
