using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;

public class CipherOrganizationPermissionsQuery : IQuery<OrganizationCipherPermission>
{
    private readonly Guid _organizationId;
    private readonly Guid _userId;

    public CipherOrganizationPermissionsQuery(Guid organizationId, Guid userId)
    {
        _organizationId = organizationId;
        _userId = userId;
    }

    public IQueryable<OrganizationCipherPermission> Run(DatabaseContext dbContext)
    {
        return from c in dbContext.Ciphers

               join ou in dbContext.OrganizationUsers
                   on new { CipherUserId = c.UserId, c.OrganizationId, UserId = (Guid?)_userId } equals
                   new { CipherUserId = (Guid?)null, OrganizationId = (Guid?)ou.OrganizationId, ou.UserId, }

               join o in dbContext.Organizations
                   on new { c.OrganizationId, OuOrganizationId = ou.OrganizationId, Enabled = true } equals
                   new { OrganizationId = (Guid?)o.Id, OuOrganizationId = o.Id, o.Enabled }

               join cc in dbContext.CollectionCiphers
                   on c.Id equals cc.CipherId into cc_g
               from cc in cc_g.DefaultIfEmpty()

               join cu in dbContext.CollectionUsers
                   on new { cc.CollectionId, OrganizationUserId = ou.Id } equals
                   new { cu.CollectionId, cu.OrganizationUserId } into cu_g
               from cu in cu_g.DefaultIfEmpty()

               join gu in dbContext.GroupUsers
                   on new { CollectionId = (Guid?)cu.CollectionId, OrganizationUserId = ou.Id } equals
                   new { CollectionId = (Guid?)null, gu.OrganizationUserId } into gu_g
               from gu in gu_g.DefaultIfEmpty()

               join g in dbContext.Groups
                   on gu.GroupId equals g.Id into g_g
               from g in g_g.DefaultIfEmpty()

               join cg in dbContext.CollectionGroups
                   on new { cc.CollectionId, gu.GroupId } equals
                   new { cg.CollectionId, cg.GroupId } into cg_g
               from cg in cg_g.DefaultIfEmpty()

               select new OrganizationCipherPermission()
               {
                   Id = c.Id,
                   OrganizationId = o.Id,
                   Read = cu != null || cg != null,
                   ViewPassword = !((bool?)cu.HidePasswords ?? (bool?)cg.HidePasswords ?? true),
                   Edit = !((bool?)cu.ReadOnly ?? (bool?)cg.ReadOnly ?? true),
                   Manage = (bool?)cu.Manage ?? (bool?)cg.Manage ?? false,
                   Unassigned = !dbContext.CollectionCiphers.Any(cc => cc.CipherId == c.Id)
               };
    }
}
