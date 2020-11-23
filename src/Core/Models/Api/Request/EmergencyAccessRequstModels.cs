using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Request
{
    public class EmergencyAccessInviteRequestModel
    {
        [Required]
        [EmailAddress]
        [StringLength(50)]
        public string Email { get; set; }
        [Required]
        public Enums.EmergencyAccessType? Type { get; set; }
        [Required]
        public int WaitTimeDays { get; set; }
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
            if (!string.IsNullOrEmpty(existingEmergencyAccess.KeyEncrypted) && !string.IsNullOrEmpty(KeyEncrypted))
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
