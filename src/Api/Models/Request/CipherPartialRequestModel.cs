using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request;

public class CipherPartialRequestModel
{
    [StringLength(36)]
    public string FolderId { get; set; }
    public bool Favorite { get; set; }
}
