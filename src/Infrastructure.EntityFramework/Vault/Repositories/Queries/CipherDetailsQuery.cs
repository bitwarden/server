using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;

public class CipherDetailsQuery : IQuery<CipherDetails>
{
    private readonly Guid? _userId;
    private readonly bool _ignoreFolders;

    public CipherDetailsQuery(Guid? userId, bool ignoreFolders = false)
    {
        _userId = userId;
        _ignoreFolders = ignoreFolders;
    }

    public virtual IQueryable<CipherDetails> Run(DatabaseContext dbContext)
    {
        // No user context: we can't resolve per-user favorites/folders/archive.
        if (!_userId.HasValue)
        {
            var query = from c in dbContext.Ciphers
                        select new CipherDetails
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
                            Reprompt = c.Reprompt,
                            Key = c.Key,
                            Favorite = false,
                            FolderId = null,
                            ArchivedDate = null,
                        };

            return query;
        }

        var userId = _userId.Value;

        var queryWithArchive =
            from c in dbContext.Ciphers
            join ca in dbContext.CipherArchives
                on new { CipherId = c.Id, UserId = userId }
                equals new { CipherId = ca.CipherId, ca.UserId }
                into caGroup
            from ca in caGroup.DefaultIfEmpty()
            select new CipherDetails
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
                Reprompt = c.Reprompt,
                Key = c.Key,
                Favorite = c.Favorites != null &&
                           c.Favorites.ToLowerInvariant().Contains($"\"{userId}\":true"),
                FolderId = (_ignoreFolders ||
                            c.Folders == null ||
                            !c.Folders.ToLowerInvariant().Contains(userId.ToString()))
                    ? null
                    : CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(c.Folders)[userId],
                ArchivedDate = ca.ArchivedDate,
            };

        return queryWithArchive;
    }
}
