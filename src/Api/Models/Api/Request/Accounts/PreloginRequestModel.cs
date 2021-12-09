using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class PreloginRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; }
    }
}
