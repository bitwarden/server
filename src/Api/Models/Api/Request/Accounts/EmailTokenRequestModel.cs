using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Web.Models.Api;

namespace Bit.Web.Models.Api
{
    public class EmailTokenRequestModel : SecretVerificationRequestModel
    {
        [Required]
        [StrictEmailAddress]
        [StringLength(256)]
        public string NewEmail { get; set; }
    }
}
