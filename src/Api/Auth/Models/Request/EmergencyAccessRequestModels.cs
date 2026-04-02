using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request;

public class EmergencyAccessInviteRequestModel
{
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public required string Email { get; set; }
    [Required]
    public EmergencyAccessType? Type { get; set; }
    [Required]
    [Range(1, short.MaxValue)]
    public int WaitTimeDays { get; set; }
}

public class EmergencyAccessUpdateRequestModel
{
    [Required]
    public EmergencyAccessType Type { get; set; }
    [Required]
    [Range(1, short.MaxValue)]
    public int WaitTimeDays { get; set; }
    public required string KeyEncrypted { get; set; }

    public EmergencyAccess ToEmergencyAccess(EmergencyAccess existingEmergencyAccess)
    {
        // Ensure we only set keys for a confirmed emergency access.
        if (!string.IsNullOrWhiteSpace(existingEmergencyAccess.KeyEncrypted) && !string.IsNullOrWhiteSpace(KeyEncrypted))
        {
            existingEmergencyAccess.KeyEncrypted = KeyEncrypted;
        }
        existingEmergencyAccess.Type = Type;
        existingEmergencyAccess.WaitTimeDays = (short)WaitTimeDays;
        return existingEmergencyAccess;
    }
}

public class EmergencyAccessPasswordRequestModel
{
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    public string? Key { get; set; }

    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }

    public bool HasNewPayloads()
    {
        return UnlockData is not null && AuthenticationData is not null;
    }
}

public class EmergencyAccessWithIdRequestModel : EmergencyAccessUpdateRequestModel
{
    [Required]
    public Guid Id { get; set; }
}
