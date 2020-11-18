using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Request
{
    public class EmergencyAccessInviteRequestModel : IValidatableObject
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public Enums.EmergencyAccessType? Type { get; set; }
        [Required]
        public int WaitTimeDays { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {

            if (Email.Contains(" ") || Email.Contains("<"))
            {
                yield return new ValidationResult($"Email is not valid.",
                    new string[] { nameof(Email) });
            }
            else if (Email.Length > 50)
            {
                yield return new ValidationResult($"Email is longer than 50 characters.",
                    new string[] { nameof(Email) });
            }
        }
    }

    public class EmergencyAccessConfirmRequestModel
    {
        [Required]
        public string Key { get; set; }
    }
    
    public class EmergencyAccessUpdateRequestModel
    {
        [Required]
        public Enums.EmergencyAccessType Type { get; set; }
        [Required]
        public int WaitTimeDays { get; set; }
        public string KeyEncrypted { get; set; }
        public EmergencyAccess ToEmergencyAccess(EmergencyAccess existingEmergencyAccess)
        {
            // Ensure we only set keys for a confirmed emergency access.
            if (!String.IsNullOrEmpty(existingEmergencyAccess.KeyEncrypted) && !String.IsNullOrEmpty(KeyEncrypted))
            {
                existingEmergencyAccess.KeyEncrypted = KeyEncrypted;
            }
            existingEmergencyAccess.Type = Type;
            existingEmergencyAccess.WaitTimeDays = WaitTimeDays;
            return existingEmergencyAccess;
        }
    }

    public class EmergencyAccessPasswordRequestModel
    {
        [Required]
        [StringLength(300)]
        public string NewMasterPasswordHash { get; set; }
        [Required]
        public string Key { get; set; }
    }
}
