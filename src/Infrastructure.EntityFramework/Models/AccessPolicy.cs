using AutoMapper;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EntityFramework.Models;

public class AccessPolicy : Core.Entities.AccessPolicy
{
    public virtual OrganizationUser OrganizationUser { get; set; }
    public virtual Group Group { get; set; }
    public virtual ServiceAccount ServiceAccount { get; set; }

    public virtual Project GrantedProject { get; set; }
    public virtual Secret GrantedServiceAccount { get; set; }
}

public class AccessPolicyMapperProfile : Profile
{
    public AccessPolicyMapperProfile()
    {
        CreateMap<Core.Entities.AccessPolicy, AccessPolicy>().ReverseMap();
    }
}


public class UserProjectAccessPolicy : BaseAccessPolicy
{
    public virtual OrganizationUser OrganizationUser { get; set; }

    public Guid? GrantedProjectId { get; set; }
}

public class UserServiceAccountAccessPolicy : BaseAccessPolicy
{
    public Guid? OrganizationUserId { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
}

public class GroupProjectAccessPolicy : BaseAccessPolicy
{
    public Guid? GroupId { get; set; }
    public Guid? GrantedProjectId { get; set; }
}

public class GroupServiceAccountAccessPolicy : BaseAccessPolicy
{
    public Guid? GroupId { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
}

public class ServiceAccountProjectAccessPolicy : BaseAccessPolicy
{
    public Guid? ServiceAccountId { get; set; }
    public Guid? GrantedProjectId { get; set; }
}
