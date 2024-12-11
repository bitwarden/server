using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserUserDetailsViewQuery : IQuery<OrganizationUserUserDetails>
{
    public IQueryable<OrganizationUserUserDetails> Run(DatabaseContext dbContext)
    {
        var query =
            from ou in dbContext.OrganizationUsers
            join u in dbContext.Users on ou.UserId equals u.Id into u_g
            from u in u_g.DefaultIfEmpty()
            join su in dbContext.SsoUsers
                on new { ou.UserId, OrganizationId = (Guid?)ou.OrganizationId } equals new
                {
                    UserId = (Guid?)su.UserId,
                    su.OrganizationId,
                }
                into su_g
            from su in su_g.DefaultIfEmpty()
            select new
            {
                ou,
                u,
                su,
            };
        return query.Select(x => new OrganizationUserUserDetails
        {
            Id = x.ou.Id,
            UserId = x.ou.UserId,
            OrganizationId = x.ou.OrganizationId,
            Name = x.u.Name,
            Email = x.u.Email ?? x.ou.Email,
            AvatarColor = x.u.AvatarColor,
            TwoFactorProviders = x.u.TwoFactorProviders,
            Premium = x.u.Premium,
            Status = x.ou.Status,
            Type = x.ou.Type,
            ExternalId = x.ou.ExternalId,
            SsoExternalId = x.su.ExternalId,
            Permissions = x.ou.Permissions,
            ResetPasswordKey = x.ou.ResetPasswordKey,
            UsesKeyConnector = x.u != null && x.u.UsesKeyConnector,
            AccessSecretsManager = x.ou.AccessSecretsManager,
            HasMasterPassword = x.u != null && !string.IsNullOrWhiteSpace(x.u.MasterPassword),
        });
    }
}
