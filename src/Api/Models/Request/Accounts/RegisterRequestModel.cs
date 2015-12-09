using System.ComponentModel.DataAnnotations;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class RegisterRequestModel
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string MasterPasswordHash { get; set; }
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
