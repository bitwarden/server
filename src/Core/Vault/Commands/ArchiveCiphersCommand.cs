using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands.Interfaces;

public class ArchiveCiphersCommand : IArchiveCiphersCommand
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IEventService _eventService;
    private readonly IPushNotificationService _pushService;

    public ArchiveCiphersCommand(
        ICipherRepository cipherRepository,
        IEventService eventService,
        IPushNotificationService pushService
        )
    {
        _cipherRepository = cipherRepository;
        _eventService = eventService;
        _pushService = pushService;
    }

    public async Task<ICollection<CipherOrganizationDetails>> ArchiveManyAsync(IEnumerable<Guid> cipherIds, Guid archivingUserId)
    {
        var cipherIdEnumerable = cipherIds as Guid[] ?? cipherIds.ToArray();
        if (cipherIds == null || cipherIdEnumerable.Length == 0)
        {
            throw new BadRequestException("No cipher ids provided.");
        }

        var cipherIdsSet = new HashSet<Guid>(cipherIdEnumerable);

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(archivingUserId);
        var archivingCiphers = ciphers
            .Where(c => cipherIdsSet.Contains(c.Id) && c.Edit && !c.OrganizationId.HasValue)
            .Select(CipherOrganizationDetails (c) => c).ToList();

        var revisionDate = await _cipherRepository.ArchiveAsync(archivingCiphers.Select(c => c.Id), archivingUserId);

        // Adding specifyKind because revisionDate is currently coming back as Unspecified from the database
        revisionDate = DateTime.SpecifyKind(revisionDate, DateTimeKind.Utc);
        var events = archivingCiphers.Select(c =>
        {
            c.RevisionDate = revisionDate;
            c.ArchivedDate = revisionDate;
            return new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_Archived, null);
        });
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        await _pushService.PushSyncCiphersAsync(archivingUserId);

        return archivingCiphers;
    }
}
