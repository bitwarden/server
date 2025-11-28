using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Repositories.Vault.Queries;

public class CipherOrganizationDetailsReadByOrganizationIdQuery : IQuery<CipherOrganizationDetails>
{
    private readonly Guid _organizationId;
    private readonly bool _unassignedOnly;

    /// <summary>
    /// Query for retrieving ciphers organization details by organization id
    /// </summary>
    /// <param name="organizationId">The id of the organization to query</param>
    /// <param name="unassignedOnly">Only include ciphers that are not assigned to any collection</param>
    public CipherOrganizationDetailsReadByOrganizationIdQuery(Guid organizationId, bool unassignedOnly = false)
    {
        _organizationId = organizationId;
        _unassignedOnly = unassignedOnly;
    }
    public virtual IQueryable<CipherOrganizationDetails> Run(DatabaseContext dbContext)
    {
        var query = from c in dbContext.Ciphers
                    join o in dbContext.Organizations
                        on c.OrganizationId equals o.Id into o_g
                    from o in o_g.DefaultIfEmpty()
                    where c.OrganizationId == _organizationId
                    select new CipherOrganizationDetails
                    {
                        Id = c.Id,
                        UserId = c.UserId,
                        OrganizationId = c.OrganizationId,
                        Type = c.Type,
                        Data = c.Data,
                        Favorites = c.Favorites,
                        Folders = c.Folders,
                        Attachments = c.Attachments,
                        CreationDate = c.CreationDate,
                        RevisionDate = c.RevisionDate,
                        DeletedDate = c.DeletedDate,
                        OrganizationUseTotp = o.UseTotp,
                    };

        if (_unassignedOnly)
        {
            var collectionCipherIds = from cc in dbContext.CollectionCiphers
                                      join c in dbContext.Collections
                                          on cc.CollectionId equals c.Id
                                      where c.OrganizationId == _organizationId
                                      select cc.CipherId;

            query = query.Where(c => !collectionCipherIds.Contains(c.Id));
        }

        return query;
    }
}
