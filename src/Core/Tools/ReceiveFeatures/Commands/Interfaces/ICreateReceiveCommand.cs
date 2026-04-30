using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;

public interface ICreateReceiveCommand
{
    public Task<Receive> CreateAsync(Receive receive);
}
