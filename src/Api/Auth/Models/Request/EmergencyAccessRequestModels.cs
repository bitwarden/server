using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request;

public class EmergencyAccessInviteRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string Email { get; set; }

    [Required]
    public EmergencyAccessType? Type { get; set; }

    [Required]
    public int WaitTimeDays { get; set; }
}

public class EmergencyAccessUpdateRequestModel
{
    [Required]
    public EmergencyAccessType Type { get; set; }

    [Required]
    public int WaitTimeDays { get; set; }
    public string KeyEncrypted { get; set; }

    public EmergencyAccess ToEmergencyAccess(EmergencyAccess existingEmergencyAccess)
    {
        // Ensure we only set keys for a confirmed emergency access.
        if (
            !string.IsNullOrWhiteSpace(existingEmergencyAccess.KeyEncrypted)
            && !string.IsNullOrWhiteSpace(KeyEncrypted)
        )
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

public class EmergencyAccessWithIdRequestModel : EmergencyAccessUpdateRequestModel
{
    [Required]
    public Guid Id { get; set; }
}
