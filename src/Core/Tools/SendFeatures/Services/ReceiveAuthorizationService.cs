using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public class ReceiveAuthorizationService : IReceiveAuthorizationService
{
    public ReceiveAuthorizationService()
    {
    }

    public bool ReceiveCanBeAccessed(Receive receive)
    {
        var now = DateTime.UtcNow;
        if (receive.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) < now)
        {
            return false;
        }

        return true;
    }

    public bool Access(Receive receive)
    {
        return ReceiveCanBeAccessed(receive);
    }
}
