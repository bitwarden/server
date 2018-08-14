using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class RegisterRequestModel : IValidatableObject
    {
        [StringLength(50)]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
        [Required]
        [StringLength(1000)]
        public string MasterPasswordHash { get; set; }
        [StringLength(50)]
        public string MasterPasswordHint { get; set; }
        public string Key { get; set; }
        public KeysRequestModel Keys { get; set; }
        public string Token { get; set; }
        public Guid? OrganizationUserId { get; set; }
        public KdfType? Kdf { get; set; }
        public int? KdfIterations { get; set; }

        public User ToUser()
        {
            var user = new User
            {
                Name = Name,
                Email = Email,
                MasterPasswordHint = MasterPasswordHint,
                Kdf = Kdf.GetValueOrDefault(KdfType.PBKDF2),
                KdfIterations = KdfIterations.GetValueOrDefault(5000)
            };

            if(Key != null)
            {
                user.Key = Key;
            }

            if(Keys != null)
            {
                Keys.ToUser(user);
            }

            return user;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(Kdf.HasValue && KdfIterations.HasValue)
            {
                switch(Kdf.Value)
                {
                    case KdfType.PBKDF2:
                        if(KdfIterations.Value < 5000 || KdfIterations.Value > 1_000_000)
                        {
                            yield return new ValidationResult("KDF iterations must be between 5000 and 1000000.");
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
