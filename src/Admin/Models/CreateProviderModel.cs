using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Models
{
    public class CreateProviderModel
    {
        public CreateProviderModel() { }
        
        [Display(Name = "User Id")]
        public Guid? UserId { get; set; }
    }
}
