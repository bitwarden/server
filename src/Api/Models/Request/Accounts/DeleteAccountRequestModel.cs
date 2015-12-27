using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class DeleteAccountRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
