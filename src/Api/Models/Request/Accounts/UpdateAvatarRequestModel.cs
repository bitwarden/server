using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Api.Models.Request.Accounts;

public class UpdateAvatarRequestModel
{
    [StringLength(7)]
    public string AvatarColor { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.AvatarColor = AvatarColor;
        return existingUser;
    }
}
