using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class SetPasswordRequestModel
{
    [Required]
    [StringLength(300)]
    public string MasterPasswordHash { get; set; }

    [Required]
    public string Key { get; set; }

    [StringLength(50)]
    public string MasterPasswordHint { get; set; }
    public KeysRequestModel Keys { get; set; }

    [Required]
    public KdfType Kdf { get; set; }

    [Required]
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
    public string OrgIdentifier { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = Kdf;
        existingUser.KdfIterations = KdfIterations;
        existingUser.KdfMemory = KdfMemory;
        existingUser.KdfParallelism = KdfParallelism;
        existingUser.Key = Key;
        Keys?.ToUser(existingUser);
        return existingUser;
    }
}
