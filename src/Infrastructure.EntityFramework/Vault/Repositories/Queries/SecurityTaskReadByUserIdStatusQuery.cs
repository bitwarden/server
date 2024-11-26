using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;

public class SecurityTaskReadByUserIdStatusQuery : IQuery<SecurityTask>
{
    private readonly Guid _userId;
    private readonly IEnumerable<SecurityTaskStatus> _status;

    public SecurityTaskReadByUserIdStatusQuery(Guid userId, IEnumerable<SecurityTaskStatus> status = null)
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
                                cc != null &&
                                (
                                    (cu != null && cu.Manage) || (cg != null && cg.Manage)
                                )
                            )
                        ) &&
                        (
                            _status == null || !_status.Any() || _status.Contains(st.Status)
                        )
                    orderby st.CreationDate descending
                    select new SecurityTask
                    {
                        Id = st.Id,
                        OrganizationId = st.OrganizationId,
                        CipherId = st.CipherId,
                        Type = st.Type,
                        Status = st.Status,
                        CreationDate = st.CreationDate,
                        RevisionDate = st.RevisionDate
                    };

        return query;
    }
}
