using System;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class InstallationRepository : Repository<TableModel.Installation, Installation, Guid>, IInstallationRepository
    {
        public InstallationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Installations)
        { }
    }
}
