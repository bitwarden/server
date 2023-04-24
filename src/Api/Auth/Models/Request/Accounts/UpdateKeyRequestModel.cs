using System.ComponentModel.DataAnnotations;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UpdateKeyRequestModel
{
    [Required]
    [StringLength(300)]
    public string MasterPasswordHash { get; set; }
    [Required]
    public IEnumerable<CipherWithIdRequestModel> Ciphers { get; set; }
    [Required]
    public IEnumerable<FolderWithIdRequestModel> Folders { get; set; }
    public IEnumerable<SendWithIdRequestModel> Sends { get; set; }
    [Required]
    public string PrivateKey { get; set; }
    [Required]
    public string Key { get; set; }
}
