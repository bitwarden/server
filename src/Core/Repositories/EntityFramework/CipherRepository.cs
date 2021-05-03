using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Core.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;

namespace Bit.Core.Repositories.EntityFramework
{
    public class CipherRepository : Repository<TableModel.Cipher, EfModel.Cipher, Guid>, ICipherRepository
    {
        public CipherRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Ciphers)
        { }

        public override async Task<Cipher> CreateAsync(Cipher cipher)
        {
            cipher = await base.CreateAsync(cipher);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                if (cipher.OrganizationId.HasValue)
                {
                    var query = new UserBumpAccountRevisionDateByCipherId(cipher);
                    var users = query.Run(dbContext);

                    await users.ForEachAsync(e => {
                        dbContext.Entry(e).Property(p => p.AccountRevisionDate).CurrentValue = DateTime.UtcNow;
                    });
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    var user = await dbContext.Users.FindAsync(cipher.UserId);
                    user.AccountRevisionDate = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
            }
            return cipher;
        }

        public IQueryable<User> GetBumpedAccountsByCipherId(Cipher cipher)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new UserBumpAccountRevisionDateByCipherId(cipher);
                return query.Run(dbContext);
            }
        }

        public Task CreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync(CipherDetails cipher)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections, IEnumerable<CollectionCipher> collectionCiphers)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(IEnumerable<Guid> ids, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            throw new NotImplementedException();
        }

        public Task DeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task DeleteByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task DeleteByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<CipherDetails> GetByIdAsync(Guid id, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> GetCanEditByIdAsync(Guid userId, Guid cipherId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Cipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<CipherDetails>> GetManyByUserIdAsync(Guid userId, bool withOrganizations = true)
        {
            throw new NotImplementedException();
        }

        public Task<CipherOrganizationDetails> GetOrganizationDetailsByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task MoveAsync(IEnumerable<Guid> ids, Guid? folderId, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task ReplaceAsync(CipherDetails cipher)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReplaceAsync(Cipher obj, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
        }

        public Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task SoftDeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAttachmentAsync(CipherAttachment attachment)
        {
            throw new NotImplementedException();
        }

        public Task UpdateCiphersAsync(Guid userId, IEnumerable<Cipher> ciphers)
        {
            throw new NotImplementedException();
        }

        public Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite)
        {
            throw new NotImplementedException();
        }

        public Task UpdateUserKeysAndCiphersAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            throw new NotImplementedException();
        }

        public Task UpsertAsync(CipherDetails cipher)
        {
            throw new NotImplementedException();
        }

        public Task DeleteDeletedAsync(DateTime deletedDateBefore)
        {
            throw new NotImplementedException();
        }
    }
}
