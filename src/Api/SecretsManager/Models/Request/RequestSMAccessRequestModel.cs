using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class RequestSMAccessRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }

    [Required(ErrorMessage = "Add a note is a required field")]
    public string EmailContent { get; set; }
}
