using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>A rotated cipher write-back (spec <c>AcceptCipherUpdate</c>).</summary>
public class SubmitCipherUpdateRequestModel
{
    /// <summary>The rotated cipher's encrypted JSON, stored verbatim; the server never decrypts it.</summary>
    [Required]
    [StringLength(500000)]
    public string Data { get; set; } = null!;

    /// <summary>
    /// The cipher's revision date as read before rotating. A mismatch means a concurrent user edit, so the write is
    /// rejected rather than overwriting it.
    /// </summary>
    [Required]
    public DateTime? LastKnownRevisionDate { get; set; }
}
