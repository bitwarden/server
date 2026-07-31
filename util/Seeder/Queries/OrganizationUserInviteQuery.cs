using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bit.Seeder.Queries;

/// <summary>
/// Retrieves the accept-organization invite url for the provided email and organization, if an invite exists.
/// </summary>
public class OrganizationUserInviteQuery(
    DatabaseContext db,
    IOrgUserInviteTokenableFactory orgUserInviteTokenableFactory,
    IDataProtectorTokenFactory<OrgUserInviteTokenable> dataProtectorTokenizer)
    : IQuery<OrganizationUserInviteQuery.Request, OrganizationUserInviteQuery.Response>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }

        [Required]
        public required string Email { get; set; }
    }

    public class Response
    {
        public required string? Url { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        // Filtering by Email naturally restricts to invited users; accepted users have a null Email.
        var orgUser = await db.OrganizationUsers.FirstOrDefaultAsync(ou =>
            ou.OrganizationId == request.OrganizationId && ou.Email == request.Email);

        if (orgUser is null)
        {
            return new Response { Url = null };
        }

        var token = dataProtectorTokenizer.Protect(orgUserInviteTokenableFactory.CreateToken(orgUser));

        var orgName = (await db.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId))?.Name
                      ?? string.Empty;

        // An invited org user is not linked to its User (UserId is null until acceptance), so the flag reflects
        // whether a Bitwarden User already exists for the invited email -- matching SendOrganizationInvitesCommand.
        var orgUserHasExistingUser = await db.Users.AnyAsync(u => u.Email == orgUser.Email);

        var url = string.Format(CultureInfo.InvariantCulture,
            "/accept-organization?organizationId={0}&organizationUserId={1}&email={2}&organizationName={3}&token={4}&initOrganization=false&orgUserHasExistingUser={5}",
            orgUser.OrganizationId,
            orgUser.Id,
            WebUtility.UrlEncode(orgUser.Email),
            WebUtility.UrlEncode(orgName),
            WebUtility.UrlEncode(token),
            orgUserHasExistingUser);

        return new Response { Url = url };
    }
}
