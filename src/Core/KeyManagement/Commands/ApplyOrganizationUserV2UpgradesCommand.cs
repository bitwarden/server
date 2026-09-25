using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;

namespace Bit.Core.KeyManagement.Commands;

public class ApplyOrganizationUserV2UpgradesCommand : IApplyOrganizationUserV2UpgradesCommand
{
    // The message names no member. Which row is stale is not a secret, but the client learns it by reading the
    // pending upgrades again, so member data does not belong in the error.
    internal const string StaleUpgradeErrorMessage =
        "One or more members' keys changed since the pending upgrades were read. Read them again and retry.";

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

        // Checking the whole batch first prevents a rejected update from writing the ones before it. The repository
        // checks each key id again as it writes, which closes the window between this read and that write.
        var allUpgradable = requested.All(update =>
            pending.TryGetValue(update.OrganizationUserId, out var details)
            && details.UserKeyId == update.UserKeyId);

        if (!allUpgradable)
        {
            throw new BadRequestException(StaleUpgradeErrorMessage);
        }

        var updatedCount = await _organizationUserKeyRepository
            .UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, requested);

        if (updatedCount != requested.Count)
        {
            throw new BadRequestException(StaleUpgradeErrorMessage);
        }
    }
}
