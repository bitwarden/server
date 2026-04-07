using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationUserResetPasswordRequestModel : IValidatableObject
{
    public bool ResetMasterPassword { get; set; }
    public bool ResetTwoFactor { get; set; }

    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    public string? Key { get; set; }

    public MasterPasswordUnlockDataRequestModel? UnlockData;
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData;

    public RecoverAccountRequest ToCommandRequest(Guid orgId, OrganizationUser organizationUser) => new()
    {
        OrgId = orgId,
        OrganizationUser = organizationUser,
        ResetMasterPassword = ResetMasterPassword,
        ResetTwoFactor = ResetTwoFactor,
        NewMasterPasswordHash = NewMasterPasswordHash,
        Key = Key,
        UnlockData = UnlockData,
        AuthenticationData = AuthenticationData,
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = UnlockData is not null && AuthenticationData is not null;
        var hasLegacyPayloads = NewMasterPasswordHash is not null || Key is not null;

        if (hasNewPayloads && hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Cannot provide both new payloads (UnlockData/AuthenticationData) and legacy payloads (NewMasterPasswordHash/Key).",
                [nameof(UnlockData), nameof(AuthenticationData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }
    }
}
