using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries
{
    public class OrganizationUserOrganizationDetailsViewQuery : IQuery<OrganizationUserOrganizationDetails>
    {
        public IQueryable<OrganizationUserOrganizationDetails> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                        join o in dbContext.Organizations on ou.OrganizationId equals o.Id
                        join su in dbContext.SsoUsers on ou.UserId equals su.UserId into su_g
                        from su in su_g.DefaultIfEmpty()
                        join po in dbContext.ProviderOrganizations on o.Id equals po.OrganizationId into po_g
                        from po in po_g.DefaultIfEmpty()
                        join p in dbContext.Providers on po.ProviderId equals p.Id into p_g
                        from p in p_g.DefaultIfEmpty()
                        join os in dbContext.OrganizationSponsorships on ou.Id equals os.SponsoringOrganizationUserId into os_g
                        from os in os_g.DefaultIfEmpty()
                        join ss in dbContext.SsoConfigs on ou.OrganizationId equals ss.OrganizationId into ss_g
                        from ss in ss_g.DefaultIfEmpty()
                        where ((su == null || !su.OrganizationId.HasValue) || su.OrganizationId == ou.OrganizationId)
                        select new { ou, o, su, p, ss, os };

            return query.Select(x => new OrganizationUserOrganizationDetails
            {
                OrganizationId = x.ou.OrganizationId,
                UserId = x.ou.UserId,
                Name = x.o.Name,
                Enabled = x.o.Enabled,
                PlanType = x.o.PlanType,
                UsePolicies = x.o.UsePolicies,
                UseSso = x.o.UseSso,
                UseKeyConnector = x.o.UseKeyConnector,
                UseScim = x.o.UseScim,
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
                PrivateKey = x.o.PrivateKey,
                ProviderId = x.p.Id,
                ProviderName = x.p.Name,
                SsoConfig = x.ss.Data,
                FamilySponsorshipFriendlyName = x.os.FriendlyName,
                FamilySponsorshipLastSyncDate = x.os.LastSyncDate,
                FamilySponsorshipToDelete = x.os.ToDelete,
                FamilySponsorshipValidUntil = x.os.ValidUntil
            });
        }
    }
}
