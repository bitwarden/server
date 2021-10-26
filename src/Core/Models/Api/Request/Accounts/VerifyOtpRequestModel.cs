using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class VerityOtpRequestModel
    {
        [Required]
        public string Otp { get; set; }
    }
}
