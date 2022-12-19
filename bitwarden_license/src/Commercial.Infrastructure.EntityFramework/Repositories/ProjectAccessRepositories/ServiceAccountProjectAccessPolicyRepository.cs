using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories.ProjectAccessRepositories;

public class ServiceAccountProjectAccessPolicyRepository : Repository<Core.Entities.ServiceAccountProjectAccessPolicy, ServiceAccountProjectAccessPolicy, Guid>, IServiceAccountProjectAccessPolicyRepository
{
    public ServiceAccountProjectAccessPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.ServiceAccountProjectAccessPolicy)
    { }

}

