using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories.ProjectAccessRepositories;

public class UserProjectAccessPolicyRepository : Repository<Core.Entities.UserProjectAccessPolicy, UserProjectAccessPolicy, Guid>, IUserProjectAccessPolicyRepository
{
    public UserProjectAccessPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.UserProjectAccessPolicy)
    { }

}
