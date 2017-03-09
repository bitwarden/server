using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Api.Models
{
    public class UpdateProfileRequestModel
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(50)]
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
