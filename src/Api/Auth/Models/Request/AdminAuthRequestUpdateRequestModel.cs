using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

public class AdminAuthRequestUpdateRequestModel
{
    public string EncryptedUserKey { get; set; }

    [Required]
    public bool RequestApproved { get; set; }
}
