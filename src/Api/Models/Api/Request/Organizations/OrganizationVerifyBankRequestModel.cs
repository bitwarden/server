using System.ComponentModel.DataAnnotations;

namespace Bit.Web.Models.Api
{
    public class OrganizationVerifyBankRequestModel
    {
        [Required]
        [Range(1, 99)]
        public int? Amount1 { get; set; }
        [Required]
        [Range(1, 99)]
        public int? Amount2 { get; set; }
    }
}
