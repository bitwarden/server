// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCreateLicenseRequestModel : LicenseRequestModel
{
    [Required]
    public string Key { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string CollectionName { get; set; }
    public OrganizationKeysRequestModel Keys { get; set; }
}
