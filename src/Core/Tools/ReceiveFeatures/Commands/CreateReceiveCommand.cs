using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class CreateReceiveCommand : ICreateReceiveCommand
{
    private readonly IReceiveRepository _receiveRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public CreateReceiveCommand(IReceiveRepository receiveRepository, IPushNotificationService pushNotificationService)
    {
        _receiveRepository = receiveRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Receive> CreateAsync(Receive receive)
    {
        var created = await _receiveRepository.CreateAsync(receive);
        await _pushNotificationService.PushSyncReceiveCreateAsync(created);
        return created;
    }
}
