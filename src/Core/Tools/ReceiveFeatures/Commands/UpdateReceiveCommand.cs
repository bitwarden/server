using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.ReceiveFeatures.Models;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class UpdateReceiveCommand : IUpdateReceiveCommand
{
    private readonly IReceiveRepository _receiveRepository;

    public UpdateReceiveCommand(IReceiveRepository receiveRepository)
    {
        _receiveRepository = receiveRepository;
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
        return receive;
    }
}
