using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public class ReceiveAuthorizationService : IReceiveAuthorizationService
{
    public ReceiveAuthorizationService()
    {
    }

    public bool ReceiveCanBeAccessed(Receive receive)
    {
        return receive.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) >= DateTime.UtcNow;
    }

    public bool Access(Receive receive)
    {
        return ReceiveCanBeAccessed(receive);
    }
}
