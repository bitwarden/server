using Bit.Core.Domains;
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models
{
    public class KeysRequestModel
    {
        public string PublicKey { get; set; }
        [Required]
        public string PrivateKey { get; set; }

        public User ToUser(User existingUser)
        {
            if(!string.IsNullOrWhiteSpace(PublicKey))
            {
                existingUser.PublicKey = PublicKey;
            }

            existingUser.PrivateKey = PrivateKey;
            return existingUser;
        }
    }
}
