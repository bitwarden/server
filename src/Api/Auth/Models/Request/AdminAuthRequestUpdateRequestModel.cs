using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

public class AdminAuthRequestUpdateRequestModel
{
    public string Key { get; set; }

    [Required]
    public bool RequestApproved { get; set; }
}
