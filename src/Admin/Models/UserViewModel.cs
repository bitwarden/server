using Bit.Core.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Admin.Models;

public class UserViewModel
{
    public UserViewModel() { }

    public UserViewModel(User user, IEnumerable<Cipher> ciphers)
    {
        User = user;
        CipherCount = ciphers.Count();
    }

    public User User { get; set; }
    public int CipherCount { get; set; }
}
