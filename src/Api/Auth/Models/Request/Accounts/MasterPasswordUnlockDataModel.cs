using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class MasterPasswordUnlockDataModel
{
    [Required]
    public KdfType KdfType { get; set; }
    [Required]
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    [Required]
    public string Email { get; set; }
    [Required]
    [StringLength(300)]
    public string MasterPasswordHash { get; set; }
    [Required]
    public string MasterKeyEncryptedUserKey { get; set; }

}
