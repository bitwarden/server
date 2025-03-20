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
        if (cipherIds == null || !cipherIds.Any())
        {
            throw new BadRequestException("No cipher ids provided.");
        }

        var cipherIdsSet = new HashSet<Guid>(cipherIds);

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(archivingUserId);
        var archivingCiphers = ciphers
            .Where(c => cipherIdsSet.Contains(c.Id) && c.Edit)
            .Select(c => (CipherOrganizationDetails)c).ToList();

        await _cipherRepository.ArchiveAsync(archivingCiphers.Select(c => c.Id), archivingUserId);

        var events = archivingCiphers.Select(c =>
            new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_Archived, null));
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        await _pushService.PushSyncCiphersAsync(archivingUserId);

        return archivingCiphers.ToList();
    }
}
