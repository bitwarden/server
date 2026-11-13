using System.Text.Json;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Tools.Services;

public class ItemSendCleanupService : IItemSendCleanupService
{
    // Bounds each lookup so a large purge does not send an unbounded id list to the database.
    private const int _batchSize = 500;

    private readonly ICipherRepository _cipherRepository;
    private readonly ISendRepository _sendRepository;
    private readonly INonAnonymousSendCommand _nonAnonymousSendCommand;

    public ItemSendCleanupService(
        ICipherRepository cipherRepository,
        ISendRepository sendRepository,
        INonAnonymousSendCommand nonAnonymousSendCommand)
    {
        _cipherRepository = cipherRepository;
        _sendRepository = sendRepository;
        _nonAnonymousSendCommand = nonAnonymousSendCommand;
    }

    public async Task DeleteItemSendsForCiphersAsync(IEnumerable<Guid> cipherIds)
    {
        foreach (var batch in cipherIds.Chunk(_batchSize))
        {
            var batchIds = batch.ToHashSet();
            var sends = await _sendRepository.GetManyItemSendsByItemIdsAsync(batchIds);
            foreach (var send in sends)
            {
                var itemId = ItemIdOf(send);
                if (itemId.HasValue && batchIds.Contains(itemId.Value))
                {
                    await _nonAnonymousSendCommand.DeleteSendAsync(send);
                }
            }
        }
    }

    public async Task DeleteItemSendsForOrganizationAsync(Guid organizationId)
    {
        var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(organizationId);
        await DeleteItemSendsForCiphersAsync(ciphers.Select(c => c.Id));
    }

    /// <summary>
    /// Reads the item id from an item-type Send's metadata. Returns null for malformed data so that a
    /// corrupt Send does not block deletion of the ciphers it references.
    /// </summary>
    private static Guid? ItemIdOf(Send send)
    {
        try
        {
            return JsonSerializer.Deserialize<SendItemData>(send.Data)?.Metadata?.ItemId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
