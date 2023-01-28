using IdentityModel;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class SubjectUserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(JwtClaimTypes.Subject)?.Value;
    }
}
