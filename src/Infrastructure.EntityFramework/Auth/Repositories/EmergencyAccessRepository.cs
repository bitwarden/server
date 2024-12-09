using AutoMapper;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class EmergencyAccessRepository : Repository<Core.Auth.Entities.EmergencyAccess, EmergencyAccess, Guid>, IEmergencyAccessRepository
{
    public EmergencyAccessRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.EmergencyAccesses)
    { }

    public async Task<int> GetCountByGrantorIdEmailAsync(Guid grantorId, string email, bool onlyRegisteredUsers)
    {
        var query = new EmergencyAccessReadCountByGrantorIdEmailQuery(grantorId, email, onlyRegisteredUsers);
        return await GetCountFromQuery(query);
    }

    public override async Task DeleteAsync(Core.Auth.Entities.EmergencyAccess emergencyAccess)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByEmergencyAccessGranteeIdAsync(emergencyAccess.Id);
            await dbContext.SaveChangesAsync();
        }
        await base.DeleteAsync(emergencyAccess);
    }

    public async Task<EmergencyAccessDetails?> GetDetailsByIdGrantorIdAsync(Guid id, Guid grantorId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new EmergencyAccessDetailsViewQuery();
            var query = view.Run(dbContext).Where(ea =>
                ea.Id == id &&
                ea.GrantorId == grantorId
            );
            return await query.FirstOrDefaultAsync();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetExpiredRecoveriesAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new EmergencyAccessDetailsViewQuery();
            var query = view.Run(dbContext).Where(ea =>
                ea.Status == EmergencyAccessStatusType.RecoveryInitiated
            );
            return await query.ToListAsync();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGranteeIdAsync(Guid granteeId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new EmergencyAccessDetailsViewQuery();
            var query = view.Run(dbContext).Where(ea =>
                ea.GranteeId == granteeId
            );
            return await query.ToListAsync();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGrantorIdAsync(Guid grantorId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new EmergencyAccessDetailsViewQuery();
            var query = view.Run(dbContext).Where(ea =>
                ea.GrantorId == grantorId
            );
            return await query.ToListAsync();
        }
    }

    public async Task<ICollection<EmergencyAccessNotify>> GetManyToNotifyAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new EmergencyAccessDetailsViewQuery();
            var query = view.Run(dbContext).Where(ea =>
                ea.Status == EmergencyAccessStatusType.RecoveryInitiated
            );
            var notifies = await query.Select(ea => new EmergencyAccessNotify
            {
                Id = ea.Id,
                GrantorId = ea.GrantorId,
                GranteeId = ea.GranteeId,
                Email = ea.Email,
                KeyEncrypted = ea.KeyEncrypted,
                Type = ea.Type,
                Status = ea.Status,
                WaitTimeDays = ea.WaitTimeDays,
                RecoveryInitiatedDate = ea.RecoveryInitiatedDate,
                LastNotificationDate = ea.LastNotificationDate,
                CreationDate = ea.CreationDate,
                RevisionDate = ea.RevisionDate,
                GranteeName = ea.GranteeName,
                GranteeEmail = ea.GranteeEmail,
                GrantorEmail = ea.GrantorEmail,
            }).ToListAsync();
            return notifies;
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(
        Guid grantorId, IEnumerable<Core.Auth.Entities.EmergencyAccess> emergencyAccessKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            var newKeys = emergencyAccessKeys.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            var userEmergencyAccess = await GetDbSet(dbContext)
                .Where(ea => ea.GrantorId == grantorId)
                .ToListAsync();
            var validEmergencyAccess = userEmergencyAccess
                .Where(ea => newKeys.Any(eak => eak.Id == ea.Id));

            foreach (var ea in validEmergencyAccess)
            {
                var eak = newKeys.First(eak => eak.Id == ea.Id);
                ea.KeyEncrypted = eak.KeyEncrypted;
            }

            await dbContext.SaveChangesAsync();
        };
    }

}
