using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Tools.Models.Request;

public class SendAccessRequestModel
{
    [StringLength(300)]
    public string Password { get; set; }
}
