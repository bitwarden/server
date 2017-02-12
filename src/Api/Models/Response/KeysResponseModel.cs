using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class KeysResponseModel : ResponseModel
    {
        public KeysResponseModel(User user)
            : base("keys")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            PublicKey = user.PublicKey;
            PrivateKey = user.PrivateKey;
        }

        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
    }
}
