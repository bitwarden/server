using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public record RecoverAccountRequest
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
}
