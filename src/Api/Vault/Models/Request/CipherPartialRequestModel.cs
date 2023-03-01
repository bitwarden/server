using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Vault.Models.Request;

public class CipherPartialRequestModel
{
    [StringLength(36)]
    public string FolderId { get; set; }
    public bool Favorite { get; set; }
}
