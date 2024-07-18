using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

public class UserDetails : User
{
    public bool HasPremiumAccess { get; set; }
}
