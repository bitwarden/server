using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands;

public class UnarchiveCiphersCommand : IUnarchiveCiphersCommand
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IPushNotificationService _pushService;

    public UnarchiveCiphersCommand(
        ICipherRepository cipherRepository,
        IPushNotificationService pushService
    )
    {
        _cipherRepository = cipherRepository;
        _pushService = pushService;
    }

    public async Task<ICollection<CipherDetails>> UnarchiveManyAsync(IEnumerable<Guid> cipherIds,
        Guid unarchivingUserId)
    {
        var cipherIdEnumerable = cipherIds as Guid[] ?? cipherIds.ToArray();
        if (cipherIds == null || cipherIdEnumerable.Length == 0)
            throw new BadRequestException("No cipher ids provided.");

        var cipherIdsSet = new HashSet<Guid>(cipherIdEnumerable);

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(unarchivingUserId);

        if (ciphers == null || ciphers.Count == 0)
        {
            return [];
        }

        var unarchivingCiphers = ciphers
            .Where(c => cipherIdsSet.Contains(c.Id) && c is { Edit: true, ArchivedDate: not null })
            .ToList();

        var revisionDate =
            await _cipherRepository.UnarchiveAsync(unarchivingCiphers.Select(c => c.Id), unarchivingUserId);
        // Adding specifyKind because revisionDate is currently coming back as Unspecified from the database
        revisionDate = DateTime.SpecifyKind(revisionDate, DateTimeKind.Utc);

        unarchivingCiphers.ForEach(c =>
        {
            c.RevisionDate = revisionDate;
            c.ArchivedDate = null;
        });
        // Will not log an event because the archive feature is limited to individual ciphers, and event logs only apply to organization ciphers.
        // Add event logging here if this is expanded to organization ciphers in the future.

        await _pushService.PushSyncCiphersAsync(unarchivingUserId);

        return unarchivingCiphers;
    }
}
