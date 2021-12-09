using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class VerifyOTPRequestModel
    {
        [Required]
        public string OTP { get; set; }
    }
}
