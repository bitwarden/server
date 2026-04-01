using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public record RecoverAccountRequest
{
    public required Guid OrgId { get; init; }
    public required OrganizationUser OrganizationUser { get; init; }
    public required bool ResetMasterPassword { get; init; }
    public required bool ResetTwoFactor { get; init; }
    public string? NewMasterPasswordHash { get; init; }
    public string? Key { get; init; }
}
