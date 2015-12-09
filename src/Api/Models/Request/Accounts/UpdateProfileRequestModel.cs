using System.ComponentModel.DataAnnotations;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class UpdateProfileRequestModel
    {
        [Required]
        public string Name { get; set; }
        public string MasterPasswordHint { get; set; }
        [Required]
        [RegularExpression("^[a-z]{2}-[A-Z]{2}$")]
        public string Culture { get; set; }

        public User ToUser(User existingUser)
        {
            existingUser.Name = Name;
            existingUser.MasterPasswordHint = string.IsNullOrWhiteSpace(MasterPasswordHint) ? null : MasterPasswordHint;
            existingUser.Culture = Culture;

            return existingUser;
        }
    }
}
