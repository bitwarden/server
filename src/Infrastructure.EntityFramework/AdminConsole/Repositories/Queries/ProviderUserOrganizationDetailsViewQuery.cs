using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

public class ProviderUserOrganizationDetailsViewQuery : IQuery<ProviderUserOrganizationDetails>
{
    public IQueryable<ProviderUserOrganizationDetails> Run(DatabaseContext dbContext)
    {
        var query =
            from pu in dbContext.ProviderUsers
            join po in dbContext.ProviderOrganizations on pu.ProviderId equals po.ProviderId
            join o in dbContext.Organizations on po.OrganizationId equals o.Id
            join p in dbContext.Providers on pu.ProviderId equals p.Id
            select new
            {
                pu,
                po,
                o,
                p,
            };
        return query.Select(x => new ProviderUserOrganizationDetails
        {
            OrganizationId = x.po.OrganizationId,
            UserId = x.pu.UserId,
            Name = x.o.Name,
            Enabled = x.o.Enabled,
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
            UseCustomPermissions = x.o.UseCustomPermissions,
            Seats = x.o.Seats,
            MaxCollections = x.o.MaxCollections,
            MaxStorageGb = x.o.MaxStorageGb,
            Identifier = x.o.Identifier,
            Key = x.po.Key,
            Status = x.pu.Status,
            Type = x.pu.Type,
            PublicKey = x.o.PublicKey,
            PrivateKey = x.o.PrivateKey,
            ProviderId = x.p.Id,
            ProviderName = x.p.Name,
            PlanType = x.o.PlanType,
            LimitCollectionCreation = x.o.LimitCollectionCreation,
            LimitCollectionDeletion = x.o.LimitCollectionDeletion,
            AllowAdminAccessToAllCollectionItems = x.o.AllowAdminAccessToAllCollectionItems,
            UseRiskInsights = x.o.UseRiskInsights,
        });
    }
}
