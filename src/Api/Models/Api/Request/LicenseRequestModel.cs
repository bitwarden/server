using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class LicenseRequestModel
    {
        [Required]
        public IFormFile License { get; set; }
    }
}
