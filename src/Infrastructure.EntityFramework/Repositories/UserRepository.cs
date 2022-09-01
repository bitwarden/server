using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class UserRepository : Repository<Core.Entities.User, User, Guid>, IUserRepository
{
    public UserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Users)
    { }

    public async Task<Core.Entities.User> GetByEmailAsync(string email)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext).FirstOrDefaultAsync(e => e.Email == email);
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
                        EF.Functions.ILike(EF.Functions.Collate(e.Email, "default"), "a%"))
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
            return Mapper.Map<List<Core.Entities.User>>(users);
        }
    }

    public async Task<ICollection<Core.Entities.User>> GetManyByPremiumAsync(bool premium)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var users = await GetDbSet(dbContext).Where(e => e.Premium == premium).ToListAsync();
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
            return Mapper.Map<Core.Entities.User>(entity);
        }
    }

    public async Task<IEnumerable<Core.Entities.User>> GetManyAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var users = dbContext.Users.Where(x => ids.Contains(x.Id));
            return await users.ToListAsync();
        }
    }
}
