using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationCreateLicenseRequestModel : LicenseRequestModel
    {
        [Required]
        public string Key { get; set; }
    }
}
