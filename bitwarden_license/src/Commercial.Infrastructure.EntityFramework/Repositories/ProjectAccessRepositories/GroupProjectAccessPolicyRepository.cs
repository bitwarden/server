using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories.ProjectAccessRepositories;

public class GroupProjectAccessPolicyRepository : Repository<Core.Entities.GroupProjectAccessPolicy, GroupProjectAccessPolicy, Guid>, IGroupProjectAccessPolicyRepository
{
    public GroupProjectAccessPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.GroupProjectAccessPolicy)
    { }
}
