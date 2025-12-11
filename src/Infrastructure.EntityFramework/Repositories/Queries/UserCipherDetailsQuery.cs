// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Vault.Models;
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

                    where (cu == null ? (Guid?)null : cu.CollectionId) != null || (cg == null ? (Guid?)null : cg.CollectionId) != null

                    select new
                    {
                        c.Id,
                        c.UserId,
                        c.OrganizationId,
                        c.Type,
                        c.Data,
                        c.Attachments,
                        c.CreationDate,
                        c.RevisionDate,
                        c.DeletedDate,
                        c.Favorites,
                        c.Folders,
                        Edit = cu == null ? (cg != null && cg.ReadOnly == false) : cu.ReadOnly == false,
                        ViewPassword = cu == null ? (cg != null && cg.HidePasswords == false) : cu.HidePasswords == false,
                        Manage = cu == null ? (cg != null && cg.Manage == true) : cu.Manage == true,
                        OrganizationUseTotp = o.UseTotp,
                        c.Reprompt,
                        c.Key,
                        c.ArchivedDate,
                        c.Archives
                    };

        var query2 = from c in dbContext.Ciphers
                     where c.UserId == _userId
                     select new
                     {
                         c.Id,
                         c.UserId,
                         c.OrganizationId,
                         c.Type,
                         c.Data,
                         c.Attachments,
                         c.CreationDate,
                         c.RevisionDate,
                         c.DeletedDate,
                         c.Favorites,
                         c.Folders,
                         Edit = true,
                         ViewPassword = true,
                         Manage = true,
                         OrganizationUseTotp = false,
                         c.Reprompt,
                         c.Key,
                         c.ArchivedDate,
                         c.Archives
                     };

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
            Favorite = _userId.HasValue && c.Favorites != null && c.Favorites.ToLowerInvariant().Contains($"\"{_userId}\":true"),
            FolderId = GetFolderId(_userId, new Cipher { Id = c.Id, Folders = c.Folders }),
            Edit = c.Edit,
            Reprompt = c.Reprompt,
            ViewPassword = c.ViewPassword,
            Manage = c.Manage,
            OrganizationUseTotp = c.OrganizationUseTotp,
            Key = c.Key,
            ArchivedDate = GetArchivedDate(_userId, new Cipher { Id = c.Id, Archives = c.Archives })
        });
        return union;
    }

    private static DateTime? GetArchivedDate(Guid? userId, Cipher cipher)
    {
        try
        {
            if (userId.HasValue && !string.IsNullOrWhiteSpace(cipher.Archives))
            {
                var archives = JsonSerializer.Deserialize<Dictionary<Guid, DateTime>>(cipher.Archives);
                if (archives.TryGetValue(userId.Value, out var archivedDate))
                {
                    return archivedDate;
                }
            }

            return null;
        }
        catch
        {
            // Some Archives might be in an invalid format like: '{ "", "<ValidDateTime>" }'
            return null;
        }
    }

    private static Guid? GetFolderId(Guid? userId, Cipher cipher)
    {
        try
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
        catch
        {
            // Some Folders might be in an invalid format like: '{ "", "<ValidGuid>" }'
            return null;
        }
    }
}
