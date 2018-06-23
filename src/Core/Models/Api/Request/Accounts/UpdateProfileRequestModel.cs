using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class UpdateProfileRequestModel
    {
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(50)]
        public string MasterPasswordHint { get; set; }

        public User ToUser(User existingUser)
        {
            existingUser.Name = Name;
            existingUser.MasterPasswordHint = string.IsNullOrWhiteSpace(MasterPasswordHint) ? null : MasterPasswordHint;
            return existingUser;
        }
    }
}
