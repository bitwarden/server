using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;

public class SecurityTaskReadByUserIdStatusQuery : IQuery<SecurityTask>
{
    private readonly Guid _userId;
    private readonly SecurityTaskStatus? _status;

    public SecurityTaskReadByUserIdStatusQuery(Guid userId, SecurityTaskStatus? status)
    {
        _userId = userId;
        _status = status;
    }

    public IQueryable<SecurityTask> Run(DatabaseContext dbContext)
    {
        var query = from st in dbContext.SecurityTasks

                    join ou in dbContext.OrganizationUsers
                        on st.OrganizationId equals ou.OrganizationId

                    join o in dbContext.Organizations
                        on st.OrganizationId equals o.Id

                    join c in dbContext.Ciphers
                        on st.CipherId equals c.Id into c_g
                    from c in c_g.DefaultIfEmpty()

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

                    join cg in dbContext.CollectionGroups
                        on new { cc.CollectionId, gu.GroupId } equals
                        new { cg.CollectionId, cg.GroupId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()

                    where
                        ou.UserId == _userId &&
                        ou.Status == OrganizationUserStatusType.Confirmed &&
                        o.Enabled &&
                        (
                            st.CipherId == null ||
                            (
                                c != null &&
                                (
                                    (cu != null && !cu.ReadOnly) || (cg != null && !cg.ReadOnly && cu == null)
                                )
                            )
                        ) &&
                        (_status == null || st.Status == _status)
                    group st by new
                    {
                        st.Id,
                        st.OrganizationId,
                        st.CipherId,
                        st.Type,
                        st.Status,
                        st.CreationDate,
                        st.RevisionDate
                    } into g
                    select new SecurityTask
                    {
                        Id = g.Key.Id,
                        OrganizationId = g.Key.OrganizationId,
                        CipherId = g.Key.CipherId,
                        Type = g.Key.Type,
                        Status = g.Key.Status,
                        CreationDate = g.Key.CreationDate,
                        RevisionDate = g.Key.RevisionDate
                    };

        return query.OrderByDescending(st => st.CreationDate);
    }
}
