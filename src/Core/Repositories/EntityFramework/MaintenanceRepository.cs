using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class MaintenanceRepository : BaseEntityFrameworkRepository, IMaintenanceRepository
    {
        public MaintenanceRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper)
        { }

        public Task DeleteExpiredGrantsAsync()
        {
            throw new NotImplementedException();
        }

        public Task DisableCipherAutoStatsAsync()
        {
            throw new NotImplementedException();
        }

        public Task RebuildIndexesAsync()
        {
            throw new NotImplementedException();
        }

        public Task UpdateStatisticsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
