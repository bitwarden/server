using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request;

public class SendAccessRequestModel
{
    [StringLength(300)]
    public string Password { get; set; }
}
