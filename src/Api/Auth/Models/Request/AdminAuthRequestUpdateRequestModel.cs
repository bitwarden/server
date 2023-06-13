using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request;

public class AdminAuthRequestUpdateRequestModel
{
    [EncryptedString]
    public string EncryptedUserKey { get; set; }

    [Required]
    public bool RequestApproved { get; set; }
}
