using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class AuthTokenTwoFactorRequestModel
    {
        [Required]
        public string Code { get; set; }
        [Required]
        public string Provider { get; set; }
        public DeviceRequestModel Device { get; set; }
    }
}
