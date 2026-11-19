// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class SubjectUserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst("sub")?.Value;
    }
}
