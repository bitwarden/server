using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Seeder.Queries;

/// <summary>
/// Retrieves all emergency access invite urls for the provided email.
/// </summary>
public class EmergencyAccessInviteQuery(
    DatabaseContext db,
    IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> dataProtectorTokenizer)
    : IQuery<EmergencyAccessInviteQuery.Request, EmergencyAccessInviteQuery.Response>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
    }

    public class Response
    {
        public required List<string> Urls { get; set; }
    }

    public Task<Response> Execute(Request request)
    {
        var urls = db.EmergencyAccesses
            .Where(ea => ea.Email == request.Email).ToList().Select(ea =>
            {
                var token = dataProtectorTokenizer.Protect(
                    new EmergencyAccessInviteTokenable(ea, hoursTillExpiration: 1)
                );
                return string.Format(CultureInfo.InvariantCulture,
                    "/accept-emergency?id={0}&name=Dummy&email={1}&token={2}",
                    WebUtility.UrlEncode(ea.Id.ToString()),
                    WebUtility.UrlEncode(ea.Email),
                    WebUtility.UrlEncode(token));
            }).ToList();

        return Task.FromResult(new Response { Urls = urls });
    }
}
