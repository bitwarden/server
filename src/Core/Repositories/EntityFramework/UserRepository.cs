using System;
using TableModel = Bit.Core.Models.Table;
using EFModel = Bit.Core.Models.EntityFramework;
using DataModel = Bit.Core.Models.Data;
using AutoMapper;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Repositories.EntityFramework
{
    public class UserRepository : Repository<TableModel.User, EFModel.User, Guid>, IUserRepository
    {
        public UserRepository(DatabaseContext databaseContext, IMapper mapper)
            : base(databaseContext, mapper, () => databaseContext.Users)
        { }

        public async Task<TableModel.User> GetByEmailAsync(string email)
        {
            return await GetDbSet().FirstOrDefaultAsync(e => e.Email == email);
        }

        public async Task<DataModel.UserKdfInformation> GetKdfInformationByEmailAsync(string email)
        {
            return await GetDbSet().Where(e => e.Email == email)
                .Select(e => new DataModel.UserKdfInformation
                {
                    Kdf = e.Kdf,
                    KdfIterations = e.KdfIterations
                }).SingleOrDefaultAsync();
        }

        public async Task<ICollection<TableModel.User>> SearchAsync(string email, int skip, int take)
        {
            var users = await GetDbSet()
                .Where(e => email == null || e.Email.StartsWith(email))
                .OrderBy(e => e.Email)
                .Skip(skip).Take(take)
                .ToListAsync();
            return Mapper.Map<List<TableModel.User>>(users);
        }

        public async Task<ICollection<TableModel.User>> GetManyByPremiumAsync(bool premium)
        {
            var users = await GetDbSet().Where(e => e.Premium == premium).ToListAsync();
            return Mapper.Map<List<TableModel.User>>(users);
        }

        public async Task<string> GetPublicKeyAsync(Guid id)
        {
            return await GetDbSet().Where(e => e.Id == id).Select(e => e.PublicKey).SingleOrDefaultAsync();
        }

        public async Task<DateTime> GetAccountRevisionDateAsync(Guid id)
        {
            return await GetDbSet().Where(e => e.Id == id).Select(e => e.AccountRevisionDate).SingleOrDefaultAsync();
        }

        public async Task UpdateStorageAsync(Guid id)
        {
            // TODO
        }

        public async Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate)
        {
            var user = new EFModel.User
            {
                Id = id,
                RenewalReminderDate = renewalReminderDate
            };
            var set = GetDbSet();
            set.Attach(user);
            DatabaseContext.Entry(user).Property(e => e.RenewalReminderDate).IsModified = true;
            await DatabaseContext.SaveChangesAsync();
        }
    }
}
