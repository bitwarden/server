// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Tools.Models.Request;

public class SendAccessRequestModel
{
    [StringLength(300)]
    public string Password { get; set; }
}
