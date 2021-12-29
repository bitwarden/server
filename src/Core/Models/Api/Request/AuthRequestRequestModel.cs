using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class AuthRequestCreateRequestModel
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string PublicKey { get; set; }
        [Required]
        public DeviceType DeviceType { get; set; }
        [Required]
        public string DeviceIdentifier { get; set; }
    }

    public class AuthRequestUpdateRequestModel
    {
        [Required]
        public string Key { get; set; }
        [Required]
        public string DeviceIdentifier { get; set; }
    }
}
