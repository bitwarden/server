using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessPolicyUpdateRequest
{
    [Required]
    public bool Read { get; set; }

    [Required]
    public bool Write { get; set; }
}
