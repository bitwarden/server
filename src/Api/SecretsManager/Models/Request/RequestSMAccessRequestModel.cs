// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class RequestSMAccessRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }
    [Required(ErrorMessage = "Add a note is a required field")]
    [StringLength(MaxEmailContentLength)]
    public string EmailContent { get; set; }

    /// <summary>
    /// The note is interpolated into an email sent to every organization admin, so it is bounded
    /// to keep both the email and the server-side sanitization work proportionate.
    /// </summary>
    public const int MaxEmailContentLength = 1000;
}
