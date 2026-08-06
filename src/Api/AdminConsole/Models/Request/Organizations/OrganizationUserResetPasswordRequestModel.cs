using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModel : IValidatableObject
{
    public bool ResetMasterPassword { get; set; }
    public bool ResetTwoFactor { get; set; }

    [Obsolete("To be removed in PM-33141")]
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; set; }

    // Should be made required in PM-33141
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    // Should be made required in PM-33141
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }

    public bool RequestHasNewDataTypes()
    {
        return UnlockData is not null && AuthenticationData is not null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 2FA-only resets carry no password payload; password validation does not apply.
        if (!ResetMasterPassword)
        {
            yield break;
        }

        // Require at least one payload form (new or legacy) when a password reset is requested.
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

        foreach (var validationResult in MasterPasswordPayloadVariantValidator.ValidatePresence(
                     RequestHasNewDataTypes(), hasLegacyPayloads))
        {
            yield return validationResult;
        }

        if (RequestHasNewDataTypes())
        {
            foreach (var validationResult in KdfSettingsValidator.ValidateKdfAndSaltAgreement(
                         AuthenticationData!.ToData(), UnlockData!.ToData()))
            {
                yield return validationResult;
            }
        }
    }

    public RecoverAccountRequest ToCommandRequest(Guid orgId, OrganizationUser organizationUser) => new()
    {
        OrgId = orgId,
        OrganizationUser = organizationUser,
        ResetMasterPassword = ResetMasterPassword,
        ResetTwoFactor = ResetTwoFactor,
        NewMasterPasswordHash = NewMasterPasswordHash,
        Key = Key,
        AuthenticationData = AuthenticationData?.ToData(),
        UnlockData = UnlockData?.ToData()
    };
}
