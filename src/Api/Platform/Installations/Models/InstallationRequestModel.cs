using System.ComponentModel.DataAnnotations;
using Bit.Core.Platform.Installations;
using Bit.Core.Utilities;

namespace Bit.Api.Platform.Installations;

public class InstallationRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }

    public Installation ToInstallation()
    {
        return new Installation
        {
            Key = CoreHelpers.SecureRandomString(20),
            Email = Email,
            Enabled = true
        };
    }
}
