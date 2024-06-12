using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class RequestSMAccessRequestModel
{
    [Required]
    public string OrganizationId { get; set; }
    [Required]
    public string EmailContent { get; set; }
}
