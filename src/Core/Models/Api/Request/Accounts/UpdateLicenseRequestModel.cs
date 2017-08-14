using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class UpdateLicenseRequestModel
    {
        [Required]
        public IFormFile License { get; set; }
    }
}
