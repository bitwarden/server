using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Api.Models.Request
{
    public class AuthRequestCreateRequestModel
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public string PublicKey { get; set; }
        [Required]
        public string DeviceIdentifier { get; set; }
        [Required]
        public AuthRequestType? Type { get; set; }
    }

    public class AuthRequestUpdateRequestModel
    {
        [Required]
        public string Key { get; set; }
        public string MasterPasswordHash { get; set; }
        [Required]
        public string DeviceIdentifier { get; set; }
    }
}
