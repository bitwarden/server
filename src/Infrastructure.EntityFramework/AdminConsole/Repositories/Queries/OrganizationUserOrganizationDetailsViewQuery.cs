using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserOrganizationDetailsViewQuery : IQuery<OrganizationUserOrganizationDetails>
{
    public IQueryable<OrganizationUserOrganizationDetails> Run(DatabaseContext dbContext)
    {
        var query = from ou in dbContext.OrganizationUsers
                    join o in dbContext.Organizations on ou.OrganizationId equals o.Id into outerOrganization
                    from o in outerOrganization.DefaultIfEmpty()
                    join su in dbContext.SsoUsers on new { ou.UserId, OrganizationId = (Guid?)ou.OrganizationId } equals new { UserId = (Guid?)su.UserId, su.OrganizationId } into su_g
                    from su in su_g.DefaultIfEmpty()
                    join po in dbContext.ProviderOrganizations on o.Id equals po.OrganizationId into po_g
                    from po in po_g.DefaultIfEmpty()
                    join p in dbContext.Providers on po.ProviderId equals p.Id into p_g
                    from p in p_g.DefaultIfEmpty()
                    join ss in dbContext.SsoConfigs on ou.OrganizationId equals ss.OrganizationId into ss_g
                    from ss in ss_g.DefaultIfEmpty()
                    join os in dbContext.OrganizationSponsorships on ou.Id equals os.SponsoringOrganizationUserId into os_g
                    from os in os_g.DefaultIfEmpty()
                    select new OrganizationUserOrganizationDetails
                    {
                        UserId = ou.UserId,
                        OrganizationId = ou.OrganizationId,
                        OrganizationUserId = ou.Id,
                        Name = o.Name,
                        Enabled = o.Enabled,
                        PlanType = o.PlanType,
                        UsePolicies = o.UsePolicies,
                        UseSso = o.UseSso,
                        UseKeyConnector = o.UseKeyConnector,
                        UseScim = o.UseScim,
                        UseGroups = o.UseGroups,
                        UseDirectory = o.UseDirectory,
                        UseEvents = o.UseEvents,
                        UseTotp = o.UseTotp,
                        Use2fa = o.Use2fa,
                        UseApi = o.UseApi,
                        UseResetPassword = o.UseResetPassword,
                        UseSecretsManager = o.UseSecretsManager,
                        SelfHost = o.SelfHost,
                        UsersGetPremium = o.UsersGetPremium,
                        UseCustomPermissions = o.UseCustomPermissions,
                        Seats = o.Seats,
                        MaxCollections = o.MaxCollections,
                        MaxStorageGb = o.MaxStorageGb,
                        Identifier = o.Identifier,
                        Key = ou.Key,
                        ResetPasswordKey = ou.ResetPasswordKey,
                        PublicKey = o.PublicKey,
                        PrivateKey = o.PrivateKey,
                        Status = ou.Status,
                        Type = ou.Type,
                        SsoExternalId = su.ExternalId,
                        Permissions = ou.Permissions,
                        ProviderId = p.Id,
                        ProviderName = p.Name,
                        ProviderType = p.Type,
                        SsoConfig = ss.Data,
                        FamilySponsorshipFriendlyName = os.FriendlyName,
                        FamilySponsorshipLastSyncDate = os.LastSyncDate,
                        FamilySponsorshipToDelete = os.ToDelete,
                        FamilySponsorshipValidUntil = os.ValidUntil,
                        AccessSecretsManager = ou.AccessSecretsManager,
                        UsePasswordManager = o.UsePasswordManager,
                        SmSeats = o.SmSeats,
                        SmServiceAccounts = o.SmServiceAccounts,
                        LimitCollectionCreation = o.LimitCollectionCreation,
                        LimitCollectionDeletion = o.LimitCollectionDeletion,
                        // Deprecated: https://bitwarden.atlassian.net/browse/PM-10863
                        LimitCollectionCreationDeletion = o.LimitCollectionCreationDeletion,
                        AllowAdminAccessToAllCollectionItems = o.AllowAdminAccessToAllCollectionItems,
                    };
        return query;
    }
}
