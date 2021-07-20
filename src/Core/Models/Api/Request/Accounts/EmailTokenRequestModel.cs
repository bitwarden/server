using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class EmailTokenRequestModel
    {
        [Required]
        [StrictEmailAddress]
        [StringLength(256)]
        public string NewEmail { get; set; }
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
    }
}
