using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class DeleteAccountRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
