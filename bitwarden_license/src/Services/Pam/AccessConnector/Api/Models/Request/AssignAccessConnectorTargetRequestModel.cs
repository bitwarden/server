using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Api.Models.Request;

public class AssignAccessConnectorTargetRequestModel
{
    /// <summary>
    /// Assignment makes the target's rotation jobs visible to the access connector. A manual target cannot be
    /// assigned.
    /// </summary>
    [Required]
    public Guid TargetSystemId { get; set; }
}
