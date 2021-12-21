using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Bit.Api.Models.Request
{
    public class LicenseRequestModel
    {
        [Required]
        public IFormFile License { get; set; }
    }
}
