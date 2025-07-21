// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
