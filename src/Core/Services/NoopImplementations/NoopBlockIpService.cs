namespace Bit.Core.Services;

public class NoopBlockIpService : IBlockIpService
{
    public Task BlockIpAsync(string ipAddress, bool permanentBlock)
    {
        // Do nothing
        return Task.FromResult(0);
    }
}
