using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Opaque;

public class OpaqueSetRegistrationActiveRequest
{
    [Required]
    public Guid SessionId { get; set; }
}
