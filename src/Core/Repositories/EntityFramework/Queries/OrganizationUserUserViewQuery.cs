using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserUserDetailsViewQuery : IQuery<OrganizationUserUserDetails>
    {
        public IQueryable<OrganizationUserUserDetails> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                        join u in dbContext.Users on ou.UserId equals u.Id into u_g
                        from u in u_g.DefaultIfEmpty()
                        join su in dbContext.SsoUsers on u.Id equals su.UserId into su_g
                        from su in su_g.DefaultIfEmpty()
                        select new { ou, u, su };
            return query.Select(x => new OrganizationUserUserDetails
            {
                Id = x.ou.Id,
                OrganizationId = x.ou.OrganizationId,
                UserId = x.ou.UserId,
                Name = x.u.Name,
                Email = x.u.Email ?? x.ou.Email,
                TwoFactorProviders = x.u.TwoFactorProviders,
                Premium = x.u.Premium,
                Status = x.ou.Status,
                Type = x.ou.Type,
                AccessAll = x.ou.AccessAll,
                ExternalId = x.ou.ExternalId,
                SsoExternalId = x.su.ExternalId,
                Permissions = x.ou.Permissions,
                ResetPasswordKey = x.ou.ResetPasswordKey,
                UsesKeyConnector = x.u.UsesKeyConnector,
            });
        }
    }
}
