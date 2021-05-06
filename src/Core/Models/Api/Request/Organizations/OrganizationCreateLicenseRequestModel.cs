using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class OrganizationCreateLicenseRequestModel : LicenseRequestModel
    {
        [Required]
        public string Key { get; set; }
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string CollectionName { get; set; }
        public OrganizationKeysRequestModel Keys { get; set; }
    }
}
