// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
    public string Email { get; set; }
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
    public string KeyEncrypted { get; set; }

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

public class EmergencyAccessPasswordRequestModel : IValidatableObject
{
    [Obsolete("To be removed in PM-33141")]
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    public string Key { get; set; }

    // Should be made required in PM-33141
    public MasterPasswordUnlockDataRequestModel UnlockData { get; set; }
    // Should be made required in PM-33141
    public MasterPasswordAuthenticationDataRequestModel AuthenticationData { get; set; }

    public bool RequestHasNewDataTypes()
    {
        return UnlockData is not null && AuthenticationData is not null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = UnlockData is not null && AuthenticationData is not null;
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

        foreach (var validationResult in MasterPasswordPayloadVariantValidator.ValidateExclusivity(
                     hasNewPayloads, hasLegacyPayloads))
        {
            yield return validationResult;
        }

        if (hasNewPayloads && !hasLegacyPayloads)
        {
            foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                         AuthenticationData.ToData(), UnlockData.ToData()))
            {
                yield return validationResult;
            }
        }
    }
}

public class EmergencyAccessWithIdRequestModel : EmergencyAccessUpdateRequestModel
{
    [Required]
    public Guid Id { get; set; }
}
