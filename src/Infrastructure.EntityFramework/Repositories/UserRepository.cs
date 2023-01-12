using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class UserRepository : Repository<Core.Entities.User, User, Guid>, IUserRepository
{
    private readonly IDataProtector _dataProtector;

    public UserRepository(
        IServiceScopeFactory serviceScopeFactory,
        IMapper mapper,
        IDataProtectionProvider dataProtectionProvider)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Users)
    {
        _dataProtector = dataProtectionProvider.CreateProtector("UserRepositoryProtection");
    }

    public override async Task<Core.Entities.User> GetByIdAsync(Guid id)
    {
        var user = await base.GetByIdAsync(id);
        UnprotectData(user);
        return user;
    }

    public async Task<Core.Entities.User> GetByEmailAsync(string email)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext).FirstOrDefaultAsync(e => e.Email == email);
            UnprotectData(entity);
            return Mapper.Map<Core.Entities.User>(entity);
        }
    }

    public async Task<DataModel.UserKdfInformation> GetKdfInformationByEmailAsync(string email)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext).Where(e => e.Email == email)
                .Select(e => new DataModel.UserKdfInformation
                {
                    Kdf = e.Kdf,
                    KdfIterations = e.KdfIterations
                }).SingleOrDefaultAsync();
        }
    }

    public async Task<ICollection<Core.Entities.User>> SearchAsync(string email, int skip, int take)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            List<User> users;
            if (dbContext.Database.IsNpgsql())
            {
                users = await GetDbSet(dbContext)
                    .Where(e => e.Email == null ||
                        EF.Functions.ILike(EF.Functions.Collate(e.Email, "default"), $"{email}%"))
                    .OrderBy(e => e.Email)
                    .Skip(skip).Take(take)
                    .ToListAsync();
            }
            else
            {
                users = await GetDbSet(dbContext)
                    .Where(e => email == null || e.Email.StartsWith(email))
                    .OrderBy(e => e.Email)
                    .Skip(skip).Take(take)
                    .ToListAsync();
            }
            UnprotectData(users);
            return Mapper.Map<List<Core.Entities.User>>(users);
        }
    }

    public async Task<ICollection<Core.Entities.User>> GetManyByPremiumAsync(bool premium)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var users = await GetDbSet(dbContext).Where(e => e.Premium == premium).ToListAsync();
            UnprotectData(users);
            return Mapper.Map<List<Core.Entities.User>>(users);
        }
    }

    public async Task<string> GetPublicKeyAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext).Where(e => e.Id == id).Select(e => e.PublicKey).SingleOrDefaultAsync();
        }
    }

    public async Task<DateTime> GetAccountRevisionDateAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext).Where(e => e.Id == id).Select(e => e.AccountRevisionDate)
                .SingleOrDefaultAsync();
        }
    }

    public async Task UpdateStorageAsync(Guid id)
    {
        await base.UserUpdateStorage(id);
    }

    public async Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var user = new User
            {
                Id = id,
                RenewalReminderDate = renewalReminderDate,
            };
            var set = GetDbSet(dbContext);
            set.Attach(user);
            dbContext.Entry(user).Property(e => e.RenewalReminderDate).IsModified = true;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Core.Entities.User> GetBySsoUserAsync(string externalId, Guid? organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoUser = await dbContext.SsoUsers.SingleOrDefaultAsync(e =>
                e.OrganizationId == organizationId && e.ExternalId == externalId);

            if (ssoUser == null)
            {
                return null;
            }

            var entity = await dbContext.Users.SingleOrDefaultAsync(e => e.Id == ssoUser.UserId);
            UnprotectData(entity);
            return Mapper.Map<Core.Entities.User>(entity);
        }
    }

    public async Task<IEnumerable<Core.Entities.User>> GetManyAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var users = await dbContext.Users.Where(x => ids.Contains(x.Id)).ToListAsync();
            UnprotectData(users);
            return users;
        }
    }

    public override async Task DeleteAsync(Core.Entities.User user)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var transaction = await dbContext.Database.BeginTransactionAsync();

            dbContext.Ciphers.RemoveRange(dbContext.Ciphers.Where(c => c.UserId == user.Id));
            dbContext.Folders.RemoveRange(dbContext.Folders.Where(f => f.UserId == user.Id));
            dbContext.Devices.RemoveRange(dbContext.Devices.Where(d => d.UserId == user.Id));
            var collectionUsers = from cu in dbContext.CollectionUsers
                                  join ou in dbContext.OrganizationUsers on cu.OrganizationUserId equals ou.Id
                                  where ou.UserId == user.Id
                                  select cu;
            dbContext.CollectionUsers.RemoveRange(collectionUsers);
            var groupUsers = from gu in dbContext.GroupUsers
                             join ou in dbContext.OrganizationUsers on gu.OrganizationUserId equals ou.Id
                             where ou.UserId == user.Id
                             select gu;
            dbContext.GroupUsers.RemoveRange(groupUsers);
            dbContext.OrganizationUsers.RemoveRange(dbContext.OrganizationUsers.Where(ou => ou.UserId == user.Id));
            dbContext.ProviderUsers.RemoveRange(dbContext.ProviderUsers.Where(pu => pu.UserId == user.Id));
            dbContext.SsoUsers.RemoveRange(dbContext.SsoUsers.Where(su => su.UserId == user.Id));
            dbContext.EmergencyAccesses.RemoveRange(
                dbContext.EmergencyAccesses.Where(ea => ea.GrantorId == user.Id || ea.GranteeId == user.Id));
            dbContext.Sends.RemoveRange(dbContext.Sends.Where(s => s.UserId == user.Id));

            var mappedUser = Mapper.Map<User>(user);
            dbContext.Users.Remove(mappedUser);

            await transaction.CommitAsync();
            await dbContext.SaveChangesAsync();
        }
    }

    public override async Task<Core.Entities.User> CreateAsync(Core.Entities.User user)
    {
        ProtectData(user);
        return await base.CreateAsync(user);
    }

    public override async Task<List<Core.Entities.User>> CreateMany(List<Core.Entities.User> users)
    {
        foreach (var user in users)
        {
            ProtectData(user);
        }
        return await base.CreateMany(users);
    }

    public override async Task ReplaceAsync(Core.Entities.User user)
    {
        ProtectData(user);
        await base.ReplaceAsync(user);
    }

    private void ProtectData(Core.Entities.User user)
    {
        user.MasterPassword = string.Concat("P_", _dataProtector.Protect(user.MasterPassword));
        user.Key = string.Concat("P_", _dataProtector.Protect(user.Key));
    }

    private void UnprotectData(Core.Entities.User user)
    {
        if (user.MasterPassword.StartsWith("P_"))
        {
            user.MasterPassword = _dataProtector.Unprotect(user.MasterPassword.Substring(2));
        }
        if (user.Key.StartsWith("P_"))
        {
            user.Key = _dataProtector.Unprotect(user.Key.Substring(2));
        }
    }

    private void UnprotectData(IEnumerable<Core.Entities.User> users)
    {
        foreach (var user in users)
        {
            UnprotectData(user);
        }
    }
}
