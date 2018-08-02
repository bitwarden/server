using IdentityModel;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Hub
{
    public class SubjectUserIdProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(JwtClaimTypes.Subject)?.Value;
        }
    }
}
