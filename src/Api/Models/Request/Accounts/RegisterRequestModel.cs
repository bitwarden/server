using System.ComponentModel.DataAnnotations;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class RegisterRequestModel
    {
        [Required]
        public string Token { get; set; }
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
        [Required]
        [StringLength(300)]
        public string MasterPasswordHash { get; set; }
        [StringLength(50)]
        public string MasterPasswordHint { get; set; }

        public User ToUser()
        {
            return new User
            {
                Name = Name,
                Email = Email,
                MasterPasswordHint = MasterPasswordHint
            };
        }
    }
}
