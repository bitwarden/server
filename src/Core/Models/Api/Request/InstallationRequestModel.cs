using Bit.Core.Models.Table;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class InstallationRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }

        public Installation ToInstallation()
        {
            return new Installation
            {
                Key = Utilities.CoreHelpers.SecureRandomString(20),
                Email = Email,
                Enabled = true
            };
        }
    }
}
