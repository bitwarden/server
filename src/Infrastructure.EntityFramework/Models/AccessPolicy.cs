using AutoMapper;
using Bit.Core.Entities;

namespace Bit.Infrastructure.EntityFramework.Models;

public class BaseAccessPolicy : Core.Entities.BaseAccessPolicy
{
    public string Discriminator { get; set; }
}

public class AccessPolicyMapperProfile : Profile
{
    public AccessPolicyMapperProfile()
    {
        CreateMap<Core.Entities.UserProjectAccessPolicy, UserProjectAccessPolicy>().ReverseMap()
            .ForMember(dst => dst.User, opt => opt.MapFrom(src => src.OrganizationUser.User));
        CreateMap<Core.Entities.GroupProjectAccessPolicy, GroupProjectAccessPolicy>().ReverseMap();
        CreateMap<Core.Entities.ServiceAccountProjectAccessPolicy, ServiceAccountProjectAccessPolicy>().ReverseMap();
    }
}

public class AccessPolicy : BaseAccessPolicy
{
}

public class UserProjectAccessPolicy : AccessPolicy, ITableObject<Guid>
{
    public Guid? OrganizationUserId { get; set; }
    public virtual OrganizationUser OrganizationUser { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public virtual Project GrantedProject { get; set; }
}

public class UserServiceAccountAccessPolicy : AccessPolicy
{
    public Guid? OrganizationUserId { get; set; }
    public virtual OrganizationUser OrganizationUser { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
    public virtual ServiceAccount GrantedServiceAccount { get; set; }
}

public class GroupProjectAccessPolicy : AccessPolicy, ITableObject<Guid>
{
    public Guid? GroupId { get; set; }
    public virtual Group Group { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public virtual Project GrantedProject { get; set; }
}

public class GroupServiceAccountAccessPolicy : AccessPolicy
{
    public Guid? GroupId { get; set; }
    public virtual Group Group { get; set; }
    public Guid? GrantedServiceAccountId { get; set; }
    public virtual ServiceAccount GrantedServiceAccount { get; set; }
}

public class ServiceAccountProjectAccessPolicy : AccessPolicy, ITableObject<Guid>
{
    public Guid? ServiceAccountId { get; set; }
    public virtual ServiceAccount ServiceAccount { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public virtual Project GrantedProject { get; set; }
}
