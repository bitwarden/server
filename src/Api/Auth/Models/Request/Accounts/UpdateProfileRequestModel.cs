using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UpdateProfileRequestModel
{
    [StringLength(50)]
    public string Name { get; set; }

    [StringLength(50)]
    [Obsolete("This field is ignored. Changes are made via the 'password' endpoint.")]
    public string MasterPasswordHint { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.Name = Name;
        return existingUser;
    }
}
