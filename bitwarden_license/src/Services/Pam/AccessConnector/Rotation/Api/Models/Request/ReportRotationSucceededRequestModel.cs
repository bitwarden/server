using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>The body of <c>POST access-connectors/rotation/attempts/{id}/success</c> (spec
/// <c>RecordRotationSucceeded</c>).</summary>
public class ReportRotationSucceededRequestModel
{
    /// <summary>
    /// The result of the rotation's optional session-termination step, recorded on the resolved attempt. A
    /// termination failure does not undo the success -- the credential was still rotated. Nullable so an omitted
    /// value is rejected rather than binding to <see cref="PamSessionTerminationOutcome.NotRequested"/>, which
    /// would record an attempted termination as one never tried.
    /// </summary>
    [Required]
    [EnumDataType(typeof(PamSessionTerminationOutcome))]
    public PamSessionTerminationOutcome? SessionTermination { get; set; }
}
