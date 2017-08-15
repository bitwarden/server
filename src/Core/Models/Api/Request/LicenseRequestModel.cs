using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class LicenseRequestModel
    {
        [Required]
        public IFormFile License { get; set; }
    }
}
