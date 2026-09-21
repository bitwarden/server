using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>The body of <c>PUT access-connectors/rotation/attempts/{id}/cipher</c> (spec
/// <c>AcceptCipherUpdate</c>).</summary>
public class SubmitCipherUpdateRequestModel
{
    /// <summary>
    /// The rotated cipher's encrypted JSON blob, written back verbatim -- opaque ciphertext to the server.
    /// </summary>
    [Required]
    [StringLength(500000)]
    public string Data { get; set; } = null!;

    /// <summary>
    /// The cipher revision date the access connector read before rotating. A mismatch at write time means a user edited
    /// the cipher concurrently, and the write is rejected rather than overwriting their change.
    /// </summary>
    [Required]
    public DateTime? LastKnownRevisionDate { get; set; }
}
