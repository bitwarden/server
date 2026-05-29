using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public record RecoverAccountRequest
{
    public required Guid OrgId { get; init; }
    public required OrganizationUser OrganizationUser { get; init; }
    public required bool ResetMasterPassword { get; init; }
    public required bool ResetTwoFactor { get; init; }
    [Obsolete("To be removed in PM-33141")]
    public string? NewMasterPasswordHash { get; init; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; init; }
    // Should be made required in PM-33141
    public MasterPasswordAuthenticationData? AuthenticationData { get; init; }
    // Should be made required in PM-33141
    public MasterPasswordUnlockData? UnlockData { get; init; }
}
