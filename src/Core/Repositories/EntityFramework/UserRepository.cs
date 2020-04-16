using System;
using TableModel = Bit.Core.Models.Table;
using EFModel = Bit.Core.Models.EntityFramework;
using DataModel = Bit.Core.Models.Data;
using AutoMapper;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Repositories.EntityFramework
{
    public class UserRepository : Repository<TableModel.User, EFModel.User, Guid>, IUserRepository
    {
        public UserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Users)
        { }

        public async Task<TableModel.User> GetByEmailAsync(string email)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                return await GetDbSet(dbContext).FirstOrDefaultAsync(e => e.Email == email);
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

        public async Task<ICollection<TableModel.User>> SearchAsync(string email, int skip, int take)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var users = await GetDbSet(dbContext)
                    .Where(e => email == null || e.Email.StartsWith(email))
                    .OrderBy(e => e.Email)
                    .Skip(skip).Take(take)
                    .ToListAsync();
                return Mapper.Map<List<TableModel.User>>(users);
            }
        }

        public async Task<ICollection<TableModel.User>> GetManyByPremiumAsync(bool premium)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var users = await GetDbSet(dbContext).Where(e => e.Premium == premium).ToListAsync();
                return Mapper.Map<List<TableModel.User>>(users);
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
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ciphers = await dbContext.Ciphers.Where(e => e.UserId == id).ToListAsync();
                var storage = ciphers.Sum(e => e.AttachmentsJson?.RootElement.EnumerateArray()
                    .Sum(p => p.GetProperty("Size").GetInt64()) ?? 0);
                var user = new EFModel.User
                {
                    Id = id,
                    RevisionDate = DateTime.UtcNow,
                    Storage = storage,
                };
                var set = GetDbSet(dbContext);
                set.Attach(user);
                var entry = dbContext.Entry(user);
                entry.Property(e => e.RevisionDate).IsModified = true;
                entry.Property(e => e.Storage).IsModified = true;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var user = new EFModel.User
                {
                    Id = id,
                    RenewalReminderDate = renewalReminderDate
                };
                var set = GetDbSet(dbContext);
                set.Attach(user);
                dbContext.Entry(user).Property(e => e.RenewalReminderDate).IsModified = true;
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
