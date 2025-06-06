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
            where authRequest.UserId == userId
            where authRequest.Type == AuthRequestType.AuthenticateAndUnlock || authRequest.Type == AuthRequestType.Unlock
            where authRequest.Approved == null
            where authRequest.CreationDate.AddMinutes(expirationMinutes) > DateTime.UtcNow
            group authRequest by authRequest.RequestDeviceIdentifier into groupedRequests
            select
                (from pendingRequests in groupedRequests
                 orderby pendingRequests.CreationDate descending
                 select pendingRequests).First();

        return pendingAuthRequestQuery;
    }
}
