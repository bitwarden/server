using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreAccessPolicy = Bit.Core.Entities.AccessPolicy;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class AccessPolicyRepository : IAccessPolicyRepository
{
    public AccessPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
    {
    }

    protected Func<DatabaseContext, DbSet<AccessPolicy>> GetDbSet { get; private set; }

    public Task<CoreAccessPolicy> GetByIdAsync(Guid id) => throw new NotImplementedException();

    public Task<CoreAccessPolicy> CreateAsync(CoreAccessPolicy obj) => throw new NotImplementedException();

    public Task ReplaceAsync(CoreAccessPolicy obj) => throw new NotImplementedException();

    public Task UpsertAsync(CoreAccessPolicy obj) => throw new NotImplementedException();

    public Task DeleteAsync(CoreAccessPolicy obj) => throw new NotImplementedException();
}
