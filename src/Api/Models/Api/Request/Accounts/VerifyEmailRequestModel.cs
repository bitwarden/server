using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class VerifyEmailRequestModel
    {
        [Required]
        public string UserId { get; set; }
        [Required]
        public string Token { get; set; }
    }
}
