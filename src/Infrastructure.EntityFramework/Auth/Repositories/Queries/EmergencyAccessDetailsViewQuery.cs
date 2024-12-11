using Bit.Core.Auth.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;

public class EmergencyAccessDetailsViewQuery : IQuery<EmergencyAccessDetails>
{
    public IQueryable<EmergencyAccessDetails> Run(DatabaseContext dbContext)
    {
        var query =
            from ea in dbContext.EmergencyAccesses
            join grantee in dbContext.Users on ea.GranteeId equals grantee.Id into grantee_g
            from grantee in grantee_g.DefaultIfEmpty()
            join grantor in dbContext.Users on ea.GrantorId equals grantor.Id into grantor_g
            from grantor in grantor_g.DefaultIfEmpty()
            select new
            {
                ea,
                grantee,
                grantor,
            };
        return query.Select(x => new EmergencyAccessDetails
        {
            Id = x.ea.Id,
            GrantorId = x.ea.GrantorId,
            GranteeId = x.ea.GranteeId,
            Email = x.ea.Email,
            KeyEncrypted = x.ea.KeyEncrypted,
            Type = x.ea.Type,
            Status = x.ea.Status,
            WaitTimeDays = x.ea.WaitTimeDays,
            RecoveryInitiatedDate = x.ea.RecoveryInitiatedDate,
            LastNotificationDate = x.ea.LastNotificationDate,
            CreationDate = x.ea.CreationDate,
            RevisionDate = x.ea.RevisionDate,
            GranteeName = x.grantee.Name,
            GranteeEmail = x.grantee.Email ?? x.ea.Email,
            GrantorName = x.grantor.Name,
            GrantorEmail = x.grantor.Email,
        });
    }
}
