using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class VerifyOTPRequestModel
    {
        [Required]
        public string OTP { get; set; }
    }
}
