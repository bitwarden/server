namespace Bit.Core.Services;

public interface IBlockIpService
{
    Task BlockIpAsync(string ipAddress, bool permanentBlock);
}
