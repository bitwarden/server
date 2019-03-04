using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class ApiKeyRequestModel
    {
        [Required]
        public string MasterPasswordHash { get; set; }
    }
}
