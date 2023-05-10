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
                        Favorite = _userId.HasValue && c.Favorites != null && c.Favorites.ToLowerInvariant().Contains($"\"{_userId}\":true"),
                        FolderId = (_ignoreFolders || !_userId.HasValue || c.Folders == null || !c.Folders.ToLowerInvariant().Contains(_userId.Value.ToString())) ?
                            null :
                            CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(c.Folders)[_userId.Value],
                    };
        return query;
    }
}
