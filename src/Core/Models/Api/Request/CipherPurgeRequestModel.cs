using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class CipherPurgeRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
