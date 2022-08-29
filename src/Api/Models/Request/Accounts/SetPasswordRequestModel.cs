using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;

namespace Bit.Api.Models.Request.Accounts;

public class SetPasswordRequestModel
{
    [Required]
    [StringLength(300)]
    public string MasterPasswordHash { get; set; }
    [Required]
    public string Key { get; set; }
    [StringLength(50)]
    public string MasterPasswordHint { get; set; }
    [Required]
    public KeysRequestModel Keys { get; set; }
    [Required]
    public KdfType Kdf { get; set; }
    [Required]
    public int KdfIterations { get; set; }
    public string OrgIdentifier { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = Kdf;
        existingUser.KdfIterations = KdfIterations;
        existingUser.Key = Key;
        Keys.ToUser(existingUser);
        return existingUser;
    }
}
