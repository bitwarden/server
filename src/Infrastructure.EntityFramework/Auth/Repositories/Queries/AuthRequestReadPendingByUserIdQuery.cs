using Bit.Core.Auth.Enums;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;

public class AuthRequestReadPendingByUserIdQuery
{
    public IQueryable<AuthRequest> GetQuery(
        DatabaseContext dbContext,
        Guid userId,
        int expirationMinutes)
    {
        var pendingAuthRequestQuery =
            from authRequest in dbContext.AuthRequests
            group authRequest by authRequest.RequestDeviceIdentifier into groupedRequests
            select
                (from pendingRequests in groupedRequests
                 where pendingRequests.UserId == userId
                 where pendingRequests.Type == AuthRequestType.AuthenticateAndUnlock || pendingRequests.Type == AuthRequestType.Unlock
                 where pendingRequests.Approved == null
                 where pendingRequests.CreationDate.AddMinutes(expirationMinutes) > DateTime.UtcNow
                 orderby pendingRequests.CreationDate descending
                 select pendingRequests).First();

        return pendingAuthRequestQuery;
    }
}
