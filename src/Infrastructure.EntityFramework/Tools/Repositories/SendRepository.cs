#nullable enable

using System.Security.Cryptography;
using AutoMapper;
using Bit.Core;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.EntityFramework.Tools.Repositories;

/// <inheritdoc cref="ISendRepository"/>
public class SendRepository : Repository<Core.Tools.Entities.Send, Send, Guid>, ISendRepository
{
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<SendRepository> _logger;

    /// <summary>
    /// Initializes the <see cref="SendRepository"/>
    /// </summary>
    /// <param name="serviceScopeFactory">An IoC service locator.</param>
    /// <param name="mapper">An automapper service.</param>
    /// <param name="dataProtectionProvider">Provides the protector used to encrypt the Emails column at rest.</param>
    /// <param name="logger">Logs decryption failures with the offending Send id.</param>
    public SendRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper,
        IDataProtectionProvider dataProtectionProvider, ILogger<SendRepository> logger)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Sends)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
        _logger = logger;
    }

    public override async Task<Core.Tools.Entities.Send?> GetByIdAsync(Guid id)
    {
        var send = await base.GetByIdAsync(id);
        if (send == null)
        {
            return null;
        }

        return UnprotectData(send) ? send : null;
    }

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
        // Capture original value, protect for storage, then restore so the caller keeps plaintext.
        var emails = send.Emails;
        ProtectData(send);
        send = await base.CreateAsync(send);
        send.Emails = emails;

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

    public override async Task ReplaceAsync(Core.Tools.Entities.Send send)
    {
        // Capture original value, protect for storage, then restore so the caller keeps plaintext.
        var emails = send.Emails;
        ProtectData(send);
        await base.ReplaceAsync(send);
        send.Emails = emails;
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.DeletionDate < deletionDateBefore).ToListAsync();
            // Don't unprotect here DeleteSendsJob needs to succeed regardless of protected values
            // Only the DeletionDate needs to read, and it is not protected
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
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results).Where(UnprotectData).ToList();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.OrganizationId == organizationId).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results).Where(UnprotectData).ToList();
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
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results).Where(UnprotectData).ToList();
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
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results).Where(UnprotectData).ToList();
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Core.Tools.Entities.Send> sends)
    {
        return async (_, _) =>
        {
            // No Emails protect/unprotect needed here: this only mutates Key on tracked entities, and EF
            // writes only the changed column, so the already-protected Emails at rest is untouched. (The
            // Dapper implementation protects because it bulk-copies whole rows.)
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
        var orgUserSendIds = await dbContext.Sends.Where(s => orgUsers.Any(ou => ou.UserId == s.UserId)).Select(s => s.Id).ToListAsync();
        return Mapper.Map<List<Guid>>(orgUserSendIds);
    }

    public async Task UpdateManyDeletionDatesByIdsAsync(IEnumerable<Guid> ids, int deletionHours)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var sends = dbContext.Sends.Where(s => ids.Contains(s.Id));
        await sends.ExecuteUpdateAsync(setters => setters
            .SetProperty(s => s.DeletionDate, s => s.CreationDate.AddHours(deletionHours))
            .SetProperty(s => s.RevisionDate, DateTime.UtcNow)
        );
        var userIds = await sends.Select(s => s.User.Id).ToArrayAsync() ?? [];
        await dbContext.UserBumpManyAccountRevisionDatesAsync(userIds);
        await dbContext.SaveChangesAsync();
    }

    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByIdsAsync(IEnumerable<Guid> ids)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await dbContext.Sends.Where(s => ids.Contains(s.Id)).ToListAsync();
        return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
    }

    private void ProtectData(Core.Tools.Entities.Send send)
    {
        if (string.IsNullOrWhiteSpace(send.Emails) ||
            send.Emails.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return;
        }

        send.Emails = string.Concat(Constants.DatabaseFieldProtectedPrefix,
            _dataProtector.Protect(send.Emails));
    }

    private bool UnprotectData(Core.Tools.Entities.Send send)
    {
        if (string.IsNullOrWhiteSpace(send.Emails) || !send.Emails.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return true;
        }

        try
        {
            send.Emails = _dataProtector.Unprotect(
                send.Emails.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
            return true;
        }
        catch (CryptographicException ex)
        {
            if (send.Emails.Length == 4000)
            {
                _logger.LogError(ex, "Emails column for Send {SendId} is max length and may have been truncated.", send.Id);
            }
            else
            {
                _logger.LogError(ex, "Failed to unprotect Emails for Send {SendId}.", send.Id);
            }
            throw;
        }
    }
}
