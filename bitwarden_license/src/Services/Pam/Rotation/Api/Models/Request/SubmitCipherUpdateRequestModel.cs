using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT rotation/attempts/{id}/cipher</c> (spec <c>AcceptCipherUpdate</c>). <see cref="Data"/> is the
/// rotated cipher's encrypted JSON blob, written back verbatim -- opaque ciphertext to the server.
/// <see cref="LastKnownRevisionDate"/> must still match the cipher's current revision date at write time or the
/// write is rejected (409) as a concurrent user edit.
/// </summary>
public class SubmitCipherUpdateRequestModel
{
    /// <summary>
    /// The rotated cipher's encrypted JSON blob, written back verbatim -- opaque ciphertext to the server.
    /// </summary>
    [Required]
    [StringLength(500000)]
    public string Data { get; set; } = null!;

    /// <summary>
    /// The cipher revision date the daemon read before rotating. A mismatch at write time means a user edited
    /// the cipher concurrently, and the write is rejected rather than overwriting their change.
    /// </summary>
    [Required]
    public DateTime? LastKnownRevisionDate { get; set; }
}
