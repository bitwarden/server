using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;

namespace Bit.Admin.Models
{
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
}
