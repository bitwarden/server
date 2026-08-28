using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>The body of <c>POST rotation/attempts/{id}/success</c> (spec <c>RecordRotationSucceeded</c>).</summary>
public class ReportRotationSucceededRequestModel
{
    /// <summary>
    /// The result of the rotation's optional session-termination step, recorded on the resolved attempt. A
    /// termination failure does not undo the success -- the credential was still rotated.
    /// </summary>
    [Required]
    public PamSessionTerminationOutcome SessionTermination { get; set; }
}
