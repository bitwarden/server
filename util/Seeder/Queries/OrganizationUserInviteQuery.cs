using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bit.Seeder.Queries;

/// <summary>
/// Retrieves all organization user invite acceptance URLs for the provided email.
/// </summary>
public class OrganizationUserInviteQuery(
    DatabaseContext db,
    IDataProtectorTokenFactory<OrgUserInviteTokenable> dataProtectorTokenizer,
    GlobalSettings globalSettings)
    : IQuery<OrganizationUserInviteQuery.Request, IEnumerable<string>>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
    }

    public async Task<IEnumerable<string>> Execute(Request request)
    {
        var orgUsers = await db.OrganizationUsers
            .Include(ou => ou.Organization)
            .Where(ou => ou.Email == request.Email)
            .ToListAsync();

        var vaultWithHash = globalSettings.BaseServiceUri.VaultWithHash;

        return orgUsers.Select(orgUser =>
        {
            Core.Entities.OrganizationUser coreOrgUser = orgUser;
            var tokenable = new OrgUserInviteTokenable(coreOrgUser);
            var protectedToken = dataProtectorTokenizer.Protect(tokenable);
            var expiringToken = new ExpiringToken(protectedToken, tokenable.ExpirationDate);

            var info = new OrganizationInvitesInfo(
                orgUser.Organization,
                orgSsoEnabled: false,
                orgSsoLoginRequiredPolicyEnabled: false,
                orgUserTokenPairs: new[] { (coreOrgUser, expiringToken) },
                orgUserHasExistingUserDict: new Dictionary<Guid, bool> { [coreOrgUser.Id] = true });

            return info.GetAcceptUrl(vaultWithHash, coreOrgUser.Id);
        }).ToList();
    }
}
