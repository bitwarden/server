using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Api.Models.Request.Accounts;

public class UpdateProfileRequestModel
{
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(50)]
    [Obsolete("Changes will be made via the 'password' endpoint going forward.")]
    public string MasterPasswordHint { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.Name = Name;
        existingUser.MasterPasswordHint = string.IsNullOrWhiteSpace(MasterPasswordHint) ? null : MasterPasswordHint;
        return existingUser;
    }
}
