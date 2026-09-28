using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;

namespace Bit.Core.KeyManagement.Commands;

public class ApplyOrganizationUserV2UpgradesCommand : IApplyOrganizationUserV2UpgradesCommand
{
    private readonly IOrganizationUserKeyRepository _organizationUserKeyRepository;

    public ApplyOrganizationUserV2UpgradesCommand(IOrganizationUserKeyRepository organizationUserKeyRepository)
    {
        _organizationUserKeyRepository = organizationUserKeyRepository;
    }

    /// <inheritdoc />
    public async Task ApplyAsync(Guid organizationId, IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates)
    {
        var requested = updates.ToList();
        if (requested.Count == 0)
        {
            return;
        }

        var pending = (await _organizationUserKeyRepository
                .GetManyPendingV2UpgradesByOrganizationIdAsync(organizationId))
            .ToDictionary(details => details.OrganizationUserId);

        // A membership that moved on is dropped instead of failing the request. Its upgrade is still pending, so
        // the admin reads it again and completes it then. The repository checks each key id once more as it
        // writes, which closes the window between this read and that write.
        var upgradable = requested
            .Where(update => pending.TryGetValue(update.OrganizationUserId, out var details)
                && details.UserKeyId == update.UserKeyId)
            .ToList();

        if (upgradable.Count == 0)
        {
            return;
        }

        await _organizationUserKeyRepository.UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, upgradable);
    }
}
