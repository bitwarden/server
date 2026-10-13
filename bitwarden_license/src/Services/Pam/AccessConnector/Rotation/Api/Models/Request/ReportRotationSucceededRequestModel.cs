using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>A successful rotation report (spec <c>RecordRotationSucceeded</c>).</summary>
public class ReportRotationSucceededRequestModel
{
    /// <summary>
    /// The outcome of the optional session-termination step; a termination failure does not undo the rotation.
    /// Nullable so an omitted value is rejected rather than read as
    /// <see cref="PamSessionTerminationOutcome.NotRequested"/>.
    /// </summary>
    [Required]
    [EnumDataType(typeof(PamSessionTerminationOutcome))]
    public PamSessionTerminationOutcome? SessionTermination { get; set; }
}
