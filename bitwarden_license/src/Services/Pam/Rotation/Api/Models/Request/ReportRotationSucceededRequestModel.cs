using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>The body of <c>POST rotation/attempts/{id}/success</c> (spec <c>RecordRotationSucceeded</c>).</summary>
public class ReportRotationSucceededRequestModel
{
    [Required]
    public PamSessionTerminationOutcome SessionTermination { get; set; }
}
