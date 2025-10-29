using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Seeder.Queries;

public class EmergencyAccessInviteQuery(
    DatabaseContext db,
    IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> dataProtectorTokenizer)
    : IQuery<EmergencyAccessInviteQuery.Request, IEnumerable<string>>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
    }

    public IEnumerable<string> Execute(Request request)
    {
        var invites = db.EmergencyAccesses
            .Where(ea => ea.Email == request.Email).ToList().Select(ea =>
            {
                var token = dataProtectorTokenizer.Protect(
                    new EmergencyAccessInviteTokenable(ea, hoursTillExpiration: 1)
                );
                return $"/accept-emergency?id={ea.Id}&name=Dummy&email={ea.Email}&token={token}";
            });

        return invites;
    }
}
