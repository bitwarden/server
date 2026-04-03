using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.ReceiveFeatures.Models;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class UpdateReceiveCommand : IUpdateReceiveCommand
{
    private readonly IReceiveRepository _receiveRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public UpdateReceiveCommand(IReceiveRepository receiveRepository, IPushNotificationService pushNotificationService)
    {
        _receiveRepository = receiveRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Receive> UpdateAsync(ReceiveUpdateData updateData, Guid userId)
    {
        var receive = await _receiveRepository.GetByIdAsync(updateData.Id);
        if (receive == null || receive.UserId != userId)
        {
            throw new NotFoundException();
        }

        receive.Name = updateData.Name;
        receive.ExpirationDate = updateData.ExpirationDate;
        receive.RevisionDate = DateTime.UtcNow;

        await _receiveRepository.ReplaceAsync(receive);
        await _pushNotificationService.PushSyncReceiveUpdateAsync(receive);
        return receive;
    }
}
