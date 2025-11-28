using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;
using C = Bit.Core.Platform.Installations;
using Ef = Bit.Infrastructure.EntityFramework.Platform;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Platform;

public class InstallationRepository : Repository<C.Installation, Ef.Installation, Guid>, C.IInstallationRepository
{
    public InstallationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Installations)
    { }
}
