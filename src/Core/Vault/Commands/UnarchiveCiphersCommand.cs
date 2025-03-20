using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands.Interfaces;

public class UnarchiveCiphersCommand : IUnarchiveCiphersCommand
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IEventService _eventService;
    private readonly IPushNotificationService _pushService;

    public UnarchiveCiphersCommand(
        ICipherRepository cipherRepository,
        IEventService eventService,
        IPushNotificationService pushService
        )
    {
        _cipherRepository = cipherRepository;
        _eventService = eventService;
        _pushService = pushService;
    }

    public async Task<ICollection<CipherOrganizationDetails>> UnarchiveManyAsync(IEnumerable<Guid> cipherIds, Guid unarchivingUserId)
    {
        if (cipherIds == null || !cipherIds.Any())
        {
            throw new BadRequestException("No cipher ids provided.");
        }

        var cipherIdsSet = new HashSet<Guid>(cipherIds);

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(unarchivingUserId);
        var unarchivingCiphers = ciphers
            .Where(c => cipherIdsSet.Contains(c.Id) && c.Edit)
            .Select(c => (CipherOrganizationDetails)c).ToList();

        DateTime? revisionDate = await _cipherRepository.UnarchiveAsync(unarchivingCiphers.Select(c => c.Id), unarchivingUserId);

        var events = unarchivingCiphers.Select(c =>
        {
            c.RevisionDate = revisionDate.Value;
            c.ArchivedDate = null;
            return new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_Unarchived, null);
        });
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        await _pushService.PushSyncCiphersAsync(unarchivingUserId);

        return unarchivingCiphers.ToList();
    }
}
