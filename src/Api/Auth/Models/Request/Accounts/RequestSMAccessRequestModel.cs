using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class RequestSMAccessRequestModel
{
    [Required]
    public string OrganizationId { get; set; }
    [Required]
    public string EmailContent { get; set; }
}
