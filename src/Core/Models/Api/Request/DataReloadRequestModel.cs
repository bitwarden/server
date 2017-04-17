using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class DataReloadRequestModel
    {
        [Required]
        public IEnumerable<LoginWithIdRequestModel> Ciphers { get; set; }
        [Required]
        public IEnumerable<FolderWithIdRequestModel> Folders { get; set; }
        [Required]
        public string PrivateKey { get; set; }
    }
}
