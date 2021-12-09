using Bit.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class PushRegistrationRequestModel
    {
        [Required]
        public string DeviceId { get; set; }
        [Required]
        public string PushToken { get; set; }
        [Required]
        public string UserId { get; set; }
        [Required]
        public DeviceType Type { get; set; }
        [Required]
        public string Identifier { get; set; }
    }
}
