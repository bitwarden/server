using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class VerifyOtpRequestModel
    {
        [Required]
        public string OTP { get; set; }
    }
}
