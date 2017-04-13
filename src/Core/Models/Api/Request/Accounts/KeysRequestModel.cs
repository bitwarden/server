using Bit.Core.Models.Table;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class KeysRequestModel
    {
        public string PublicKey { get; set; }
        [Required]
        public string EncryptedPrivateKey { get; set; }

        public User ToUser(User existingUser)
        {
            if(string.IsNullOrWhiteSpace(existingUser.PublicKey) && !string.IsNullOrWhiteSpace(PublicKey))
            {
                existingUser.PublicKey = PublicKey;
            }

            if(string.IsNullOrWhiteSpace(existingUser.PrivateKey))
            {
                existingUser.PrivateKey = EncryptedPrivateKey;
            }

            return existingUser;
        }
    }
}
