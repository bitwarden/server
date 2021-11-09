using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Api
{
    public class EmailTokenRequestModel : SecretVerificationRequestModel
    {
        [Required]
        [StrictEmailAddress]
        [StringLength(256)]
        public string NewEmail { get; set; }
    }
}
