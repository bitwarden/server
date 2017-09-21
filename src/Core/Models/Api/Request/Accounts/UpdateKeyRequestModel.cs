using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class UpdateKeyRequestModel
    {
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        [Required]
        public IEnumerable<CipherWithIdRequestModel> Ciphers { get; set; }
        [Required]
        public IEnumerable<FolderWithIdRequestModel> Folders { get; set; }
        [Required]
        public string PrivateKey { get; set; }
        [Required]
        public string Key { get; set; }
    }
}
