using AutoMapper;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public class SecurityTaskRepository
    : Repository<Core.Vault.Entities.SecurityTask, SecurityTask, Guid>,
        ISecurityTaskRepository
{
    public SecurityTaskRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (context) => context.SecurityTasks) { }
}
