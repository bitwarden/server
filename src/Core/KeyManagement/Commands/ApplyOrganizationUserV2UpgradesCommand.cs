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
    public async Task ApplyAsync(Guid organizationId, bool includeOwners,
        IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates)
    {
        var requested = updates.ToList();
        if (requested.Count == 0)
        {
            return;
        }

        // One date for the rows and their events, so the audit log matches what was written.
        var revisionDate = _timeProvider.GetUtcNow().UtcDateTime;

        // A membership that moved on is skipped by the write instead of failing the request. The repository checks
        // the organization, the membership type, the token, the enrollment, and the key id as it writes each row.
        var updatedIds = (await _organizationUserKeyRepository
                .UpdateManyV2UpgradedAccountRecoveryKeysAsync(organizationId, includeOwners, requested,
                    revisionDate))
            .ToHashSet();

        await LogUnenrollmentsAsync(requested, updatedIds, revisionDate);
    }

    /// <summary>
    /// Logs a withdrawal for each member the admin unenrolled. A re-wrapped key is not logged, because the member
    /// stays enrolled and only the key that wraps their user key changes.
    /// </summary>
    private async Task LogUnenrollmentsAsync(IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> requested,
        IReadOnlySet<Guid> updatedIds, DateTime revisionDate)
    {
        var unenrolledIds = requested
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
