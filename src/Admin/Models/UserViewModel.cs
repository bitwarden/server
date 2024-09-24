using Bit.Core.Vault.Entities;

namespace Bit.Admin.Models;

public class UserViewModel
{
    public UserViewModel() { }

    public UserViewModel(UserModel user, IEnumerable<Cipher> ciphers)
    {
        User = user;
        CipherCount = ciphers.Count();
    }

    public UserModel User { get; set; }
    public int CipherCount { get; set; }
}
