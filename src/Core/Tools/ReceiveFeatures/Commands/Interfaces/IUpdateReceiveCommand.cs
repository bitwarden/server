using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Models;

namespace Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;

public interface IUpdateReceiveCommand
{
    public Task<Receive> UpdateAsync(ReceiveUpdateData updateData, Guid userId);
}
