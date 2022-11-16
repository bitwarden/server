using System.Text.Json;
using Bit.Core.Enums;
using Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class UserCipherDetailsQuery : IQuery<CipherDetails>
{
    private readonly Guid? _userId;
    public UserCipherDetailsQuery(Guid? userId)
    {
        _userId = userId;
    }
    public virtual IQueryable<CipherDetails> Run(DatabaseContext dbContext)
    {
        var query = from c in dbContext.Ciphers

                    join ou in dbContext.OrganizationUsers
                        on new { CipherUserId = c.UserId, c.OrganizationId, UserId = _userId, Status = OrganizationUserStatusType.Confirmed } equals
                           new { CipherUserId = (Guid?)null, OrganizationId = (Guid?)ou.OrganizationId, ou.UserId, ou.Status }

                    join o in dbContext.Organizations
                        on new { c.OrganizationId, OuOrganizationId = ou.OrganizationId, Enabled = true } equals
                           new { OrganizationId = (Guid?)o.Id, OuOrganizationId = o.Id, o.Enabled }

                    join cc in dbContext.CollectionCiphers
                        on new { ou.AccessAll, CipherId = c.Id } equals
                           new { AccessAll = false, cc.CipherId } into cc_g
                    from cc in cc_g.DefaultIfEmpty()

                    join cu in dbContext.CollectionUsers
                        on new { cc.CollectionId, OrganizationUserId = ou.Id } equals
                           new { cu.CollectionId, cu.OrganizationUserId } into cu_g
                    from cu in cu_g.DefaultIfEmpty()

                    join gu in dbContext.GroupUsers
                        on new { CollectionId = (Guid?)cu.CollectionId, ou.AccessAll, OrganizationUserId = ou.Id } equals
                           new { CollectionId = (Guid?)null, AccessAll = false, gu.OrganizationUserId } into gu_g
                    from gu in gu_g.DefaultIfEmpty()

                    join g in dbContext.Groups
                        on gu.GroupId equals g.Id into g_g
                    from g in g_g.DefaultIfEmpty()

                    join cg in dbContext.CollectionGroups
                        on new { g.AccessAll, cc.CollectionId, gu.GroupId } equals
                           new { AccessAll = false, cg.CollectionId, cg.GroupId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()

                    where ou.AccessAll || cu.CollectionId != null || g.AccessAll || cg.CollectionId != null

                    select new { c, ou, o, cc, cu, gu, g, cg }.c;

        var query2 = from c in dbContext.Ciphers
                     where c.UserId == _userId
                     select c;

        var union = query.Union(query2).Select(c => new CipherDetails
        {
            Id = c.Id,
            UserId = c.UserId,
            OrganizationId = c.OrganizationId,
            Type = c.Type,
            Data = c.Data,
            Attachments = c.Attachments,
            CreationDate = c.CreationDate,
            RevisionDate = c.RevisionDate,
            DeletedDate = c.DeletedDate,
            Favorite = _userId.HasValue && c.Favorites != null && c.Favorites.Contains($"\"{_userId}\":true"),
            FolderId = GetFolderId(_userId, c),
            Edit = true,
            ViewPassword = true,
            OrganizationUseTotp = false,
        });
        return union;
    }

    private static Guid? GetFolderId(Guid? userId, Models.Cipher cipher)
    {
        if (userId.HasValue && !string.IsNullOrWhiteSpace(cipher.Folders))
        {
            var folders = JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(cipher.Folders);
            if (folders.TryGetValue(userId.Value, out var folder))
            {
                return folder;
            }
        }
        return null;
    }
}
