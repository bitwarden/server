using Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class CipherOrganizationDetailsReadByOrganizationIdQuery : IQuery<CipherOrganizationDetails>
{
    private readonly Guid _organizationId;

    public CipherOrganizationDetailsReadByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
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
        return query;
    }
}
