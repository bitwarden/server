using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class EmergencyAccessRepository : Repository<TableModel.EmergencyAccess, EfModel.EmergencyAccess, Guid>, IEmergencyAccessRepository
    {
        public EmergencyAccessRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.EmergencyAccesses)
        { }

        public Task<int> GetCountByGrantorIdEmailAsync(Guid grantorId, string email, bool onlyRegisteredUsers)
        {
            throw new NotImplementedException();
        }

        public Task<EmergencyAccessDetails> GetDetailsByIdGrantorIdAsync(Guid id, Guid grantorId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<EmergencyAccessDetails>> GetExpiredRecoveriesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGranteeIdAsync(Guid granteeId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGrantorIdAsync(Guid grantorId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<EmergencyAccessNotify>> GetManyToNotifyAsync()
        {
            throw new NotImplementedException();
        }
    }
}
