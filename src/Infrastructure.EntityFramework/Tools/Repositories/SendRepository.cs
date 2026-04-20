#nullable enable

using AutoMapper;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Tools.Repositories;

/// <inheritdoc cref="ISendRepository"/>
public class SendRepository : Repository<Core.Tools.Entities.Send, Send, Guid>, ISendRepository
{
    /// <summary>
    /// Initializes the <see cref="SendRepository"/>
    /// </summary>
    /// <param name="serviceScopeFactory">An IoC service locator.</param>
    /// <param name="mapper">An automapper service.</param>
    public SendRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Sends)
    { }

    /// <summary>
    /// Saves a <see cref="Send"/> in the database.
    /// </summary>
    /// <param name="send">
    /// The send being saved.
    /// </param>
    /// <returns>
    /// A task that completes once the save is complete.
    /// The task result contains the saved <see cref="Send"/>.
    /// </returns>
    public override async Task<Core.Tools.Entities.Send> CreateAsync(Core.Tools.Entities.Send send)
    {
        send = await base.CreateAsync(send);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            if (send.UserId.HasValue)
            {
                await UserUpdateStorage(send.UserId.Value);
                await dbContext.UserBumpAccountRevisionDateAsync(send.UserId.Value);
                await dbContext.SaveChangesAsync();
            }
        }

        return send;
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.DeletionDate < deletionDateBefore).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.UserId == userId).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.OrganizationId == organizationId).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyFileSendsByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends
                .Where(s => s.UserId == userId && s.Type == SendType.File)
                .ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyFileSendsByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends
                .Where(s => s.OrganizationId == organizationId && s.Type == SendType.File)
                .ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Core.Tools.Entities.Send> sends)
    {
        return async (_, _) =>
        {
            var newSends = sends.ToDictionary(s => s.Id);
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            var userSends = await GetDbSet(dbContext)
                .Where(s => s.UserId == userId)
                .ToListAsync();
            var validSends = userSends
                .Where(send => newSends.ContainsKey(send.Id));
            foreach (var send in validSends)
            {
                send.Key = newSends[send.Id].Key;
            }

            await dbContext.SaveChangesAsync();
        };
    }

    /// <inheritdoc />  
    public async Task UpdateManyDisabledAsync(IEnumerable<Guid> ids, bool disabled)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var sends = dbContext.Sends.Where(s => ids.Contains(s.Id));
        await sends.ExecuteUpdateAsync(setters => setters
            .SetProperty(s => s.Disabled, disabled)
            .SetProperty(s => s.RevisionDate, DateTime.UtcNow)
        );
        var userIds = await sends.Select(s => s.User.Id).ToArrayAsync() ?? [];
        await dbContext.UserBumpManyAccountRevisionDatesAsync(userIds);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<Guid>> GetIdsByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var orgUsers = await dbContext.OrganizationUsers.Where(ou => ou.OrganizationId == organizationId).ToListAsync();
        var orgUserSendIds = await dbContext.Sends.Where(s => orgUsers.Any(ou => ou.Id == s.UserId)).Select(s => s.Id).ToListAsync();
        return Mapper.Map<List<Guid>>(orgUserSendIds);
    }

    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByIdsAsync(IEnumerable<Guid> ids)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await dbContext.Sends.Where(s => ids.Contains(s.Id)).ToListAsync();
        return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
    }
}
