#nullable enable
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Repositories.Queries;

public class NotificationStatusDetailsViewQuery(Guid userId, ClientType clientType)
    : IQuery<NotificationStatusDetails>
{
    public IQueryable<NotificationStatusDetails> Run(DatabaseContext dbContext)
    {
        var clientTypes = new[] { ClientType.All };
        if (clientType != ClientType.All)
        {
            clientTypes = [ClientType.All, clientType];
        }

        var query =
            from n in dbContext.Notifications
            join ou in dbContext.OrganizationUsers.Where(ou => ou.UserId == userId)
                on n.OrganizationId equals ou.OrganizationId
                into groupingOrganizationUsers
            from ou in groupingOrganizationUsers.DefaultIfEmpty()
            join ns in dbContext.NotificationStatuses.Where(ns => ns.UserId == userId)
                on n.Id equals ns.NotificationId
                into groupingNotificationStatus
            from ns in groupingNotificationStatus.DefaultIfEmpty()
            where
                clientTypes.Contains(n.ClientType)
                && (
                    (n.Global && n.UserId == null && n.OrganizationId == null)
                    || (!n.Global && n.UserId == userId && (n.OrganizationId == null || ou != null))
                    || (!n.Global && n.UserId == null && ou != null)
                )
            select new { n, ns };

        return query.Select(x => new NotificationStatusDetails
        {
            Id = x.n.Id,
            Priority = x.n.Priority,
            Global = x.n.Global,
            ClientType = x.n.ClientType,
            UserId = x.n.UserId,
            OrganizationId = x.n.OrganizationId,
            Title = x.n.Title,
            Body = x.n.Body,
            CreationDate = x.n.CreationDate,
            RevisionDate = x.n.RevisionDate,
            ReadDate = x.ns != null ? x.ns.ReadDate : null,
            DeletedDate = x.ns != null ? x.ns.DeletedDate : null,
        });
    }
}
