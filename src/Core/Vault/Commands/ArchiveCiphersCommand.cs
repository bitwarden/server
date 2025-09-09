using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands.Interfaces;

public class ArchiveCiphersCommand : IArchiveCiphersCommand
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IPushNotificationService _pushService;

    public ArchiveCiphersCommand(
        ICipherRepository cipherRepository,
        IPushNotificationService pushService
    )
    {
        _cipherRepository = cipherRepository;
        _pushService = pushService;
    }

    public async Task<ICollection<CipherOrganizationDetails>> ArchiveManyAsync(IEnumerable<Guid> cipherIds,
        Guid archivingUserId)
    {
        var cipherIdEnumerable = cipherIds as Guid[] ?? cipherIds.ToArray();
        if (cipherIds == null || cipherIdEnumerable.Length == 0)
            throw new BadRequestException("No cipher ids provided.");

        var cipherIdsSet = new HashSet<Guid>(cipherIdEnumerable);

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(archivingUserId);
        var archivingCiphers = ciphers
            .Where(c => cipherIdsSet.Contains(c.Id) && c.Edit && !c.OrganizationId.HasValue)
            .Select(CipherOrganizationDetails (c) => c).ToList();

        var revisionDate = await _cipherRepository.ArchiveAsync(archivingCiphers.Select(c => c.Id), archivingUserId);

        // Adding specifyKind because revisionDate is currently coming back as Unspecified from the database
        revisionDate = DateTime.SpecifyKind(revisionDate, DateTimeKind.Utc);

        archivingCiphers.ForEach(c =>
        {
            c.RevisionDate = revisionDate;
            c.ArchivedDate = revisionDate;
        });

        // Will not log an event because the archive feature is limited to individual ciphers, and event logs only apply to organization ciphers.
        // Add event logging here if this is expanded to organization ciphers in the future.

        await _pushService.PushSyncCiphersAsync(archivingUserId);

        return archivingCiphers;
    }
}
