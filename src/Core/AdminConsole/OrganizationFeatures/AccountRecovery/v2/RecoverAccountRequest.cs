using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public record RecoverAccountRequest : IValidatableObject
{
    public required Guid OrgId { get; init; }
    public required OrganizationUser OrganizationUser { get; init; }
    public required bool ResetMasterPassword { get; init; }
    public required bool ResetTwoFactor { get; init; }

    public MasterPasswordUnlockDataRequestModel? UnlockData;
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData;

    [Obsolete("To be removed in PM-33141")]
    public string? NewMasterPasswordHash { get; init; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; init; }

    // To be removed in PM-33141
    public bool RequestHasNewDataTypes()
    {
        return UnlockData is not null && AuthenticationData is not null;
    }

    // To be removed in PM-33141
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = UnlockData is not null && AuthenticationData is not null;
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

        if (hasNewPayloads && hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Cannot provide both new payloads (UnlockData/AuthenticationData) and legacy payloads (NewMasterPasswordHash/Key).",
                [nameof(UnlockData), nameof(AuthenticationData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }

        if (!hasNewPayloads && !hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads (NewMasterPasswordHash/Key).",
                [nameof(UnlockData), nameof(AuthenticationData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }
    }
}
