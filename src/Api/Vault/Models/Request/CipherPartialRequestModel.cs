// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Vault.Models.Request;

public class CipherPartialRequestModel
{
    [StringLength(36)]
    public string FolderId { get; set; }
    public bool Favorite { get; set; }
}
