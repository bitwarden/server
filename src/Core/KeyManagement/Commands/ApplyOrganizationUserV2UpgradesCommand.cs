using Bit.Core.Enums;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.KeyManagement.Commands;

public class ApplyOrganizationUserV2UpgradesCommand : IApplyOrganizationUserV2UpgradesCommand
{
    private readonly IOrganizationUserKeyRepository _organizationUserKeyRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;
    private readonly TimeProvider _timeProvider;

    public ApplyOrganizationUserV2UpgradesCommand(
        IOrganizationUserKeyRepository organizationUserKeyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        TimeProvider timeProvider)
    {
        _organizationUserKeyRepository = organizationUserKeyRepository;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _timeProvider = timeProvider;
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

        // One date for the rows and their events, so the audit log matches what was written.
        var revisionDate = _timeProvider.GetUtcNow().UtcDateTime;
        var updatedIds = (await _organizationUserKeyRepository
                .UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, upgradable, revisionDate))
            .ToHashSet();

        await LogUnenrollmentsAsync(upgradable, updatedIds, revisionDate);
    }

    /// <summary>
    /// Logs a withdrawal for each member the admin unenrolled. A re-wrapped key is not logged, because the member
    /// stays enrolled and only the key that wraps their user key changes.
    /// </summary>
    private async Task LogUnenrollmentsAsync(IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> upgradable,
        IReadOnlySet<Guid> updatedIds, DateTime revisionDate)
    {
        var unenrolledIds = upgradable
            .Where(update => update.AccountRecoveryKey is null && updatedIds.Contains(update.OrganizationUserId))
            .Select(update => update.OrganizationUserId)
            .ToList();

        if (unenrolledIds.Count == 0)
        {
            return;
        }

        var unenrolled = await _organizationUserRepository.GetManyAsync(unenrolledIds);
        await _eventService.LogOrganizationUserEventsAsync(unenrolled.Select(organizationUser =>
            (organizationUser, EventType.OrganizationUser_ResetPassword_Withdraw, (DateTime?)revisionDate)));
    }
}
