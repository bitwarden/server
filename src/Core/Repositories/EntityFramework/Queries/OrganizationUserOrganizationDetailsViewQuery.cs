using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserOrganizationDetailsViewQuery : IQuery<OrganizationUserOrganizationDetails>
    {
        public IQueryable<OrganizationUserOrganizationDetails> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                join o in dbContext.Organizations on ou.OrganizationId equals o.Id
                join su in dbContext.SsoUsers on ou.UserId equals su.UserId into su_g
                from su in su_g.DefaultIfEmpty()
                where ((su == null || !su.OrganizationId.HasValue) || su.OrganizationId == ou.OrganizationId)
                select new { ou, o, su };
            return query.Select(x => new OrganizationUserOrganizationDetails {
                OrganizationId = x.ou.OrganizationId,
                UserId = x.ou.UserId,
                Name = x.o.Name,
                Enabled = x.o.Enabled,
                UsePolicies = x.o.UsePolicies,
                UseSso = x.o.UseSso,
                UseGroups = x.o.UseGroups,
                UseDirectory = x.o.UseDirectory,
                UseEvents = x.o.UseEvents,
                UseTotp = x.o.UseTotp,
                Use2fa = x.o.Use2fa,
                UseApi = x.o.UseApi,
                SelfHost = x.o.SelfHost,
                UsersGetPremium = x.o.UsersGetPremium,
                Seats = x.o.Seats,
                MaxCollections = x.o.MaxCollections,
                MaxStorageGb = x.o.MaxStorageGb,
                Identifier = x.o.Identifier,
                Key = x.ou.Key,
                ResetPasswordKey = x.ou.ResetPasswordKey,
                Status = x.ou.Status,
                Type = x.ou.Type,
                SsoExternalId = x.su.ExternalId,
                Permissions = x.ou.Permissions,
                PublicKey = x.o.PublicKey,
                PrivateKey = x.o.PrivateKey
            });
        }
    }
}
