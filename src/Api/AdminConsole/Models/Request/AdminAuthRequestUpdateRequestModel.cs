// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class AdminAuthRequestUpdateRequestModel
{
    [EncryptedString]
    public string EncryptedUserKey { get; set; }

    [Required]
    public bool RequestApproved { get; set; }
}
