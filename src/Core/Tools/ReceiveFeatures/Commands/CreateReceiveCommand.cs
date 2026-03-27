using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class CreateReceiveCommand : ICreateReceiveCommand
{
    private readonly IReceiveRepository _receiveRepository;

    public CreateReceiveCommand(IReceiveRepository receiveRepository)
    {
        _receiveRepository = receiveRepository;
    }

    public async Task<Receive> CreateAsync(Receive receive)
    {
        return await _receiveRepository.CreateAsync(receive);
    }
}
