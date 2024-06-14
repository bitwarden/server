using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class RequestSMAccessRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }
    [Required]
    public string EmailContent { get; set; }
}
