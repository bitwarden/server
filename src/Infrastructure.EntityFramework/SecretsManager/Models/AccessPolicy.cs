using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class BaseAccessPolicy : Core.SecretsManager.Entities.BaseAccessPolicy
{
    public string Discriminator { get; set; }
}

public class AccessPolicyMapperProfile : Profile
{
    public AccessPolicyMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.UserProjectAccessPolicy, UserProjectAccessPolicy>()
            .ForMember(dst => dst.GrantedProject, opt => opt.Ignore())
            .ForMember(dst => dst.OrganizationUser, opt => opt.Ignore())
            .ReverseMap()
            .ForMember(dst => dst.User, opt => opt.MapFrom(src => src.OrganizationUser.User));

        CreateMap<Core.SecretsManager.Entities.UserServiceAccountAccessPolicy, UserServiceAccountAccessPolicy>()
            .ForMember(dst => dst.GrantedServiceAccount, opt => opt.Ignore())
            .ForMember(dst => dst.OrganizationUser, opt => opt.Ignore())
            .ReverseMap()
            .ForMember(dst => dst.User, opt => opt.MapFrom(src => src.OrganizationUser.User));

        CreateMap<Core.SecretsManager.Entities.GroupProjectAccessPolicy, GroupProjectAccessPolicy>()
            .ForMember(dst => dst.GrantedProject, opt => opt.Ignore())
            .ForMember(dst => dst.Group, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy, GroupServiceAccountAccessPolicy>()
            .ForMember(dst => dst.GrantedServiceAccount, opt => opt.Ignore())
            .ForMember(dst => dst.Group, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy, ServiceAccountProjectAccessPolicy>()
            .ForMember(dst => dst.GrantedProject, opt => opt.Ignore())
            .ForMember(dst => dst.ServiceAccount, opt => opt.Ignore())
            .ReverseMap();
    }
}

public class AccessPolicy : BaseAccessPolicy
{
}

public class UserProjectAccessPolicy : AccessPolicy
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

public class GroupProjectAccessPolicy : AccessPolicy
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

public class ServiceAccountProjectAccessPolicy : AccessPolicy
{
    public Guid? ServiceAccountId { get; set; }
    public virtual ServiceAccount ServiceAccount { get; set; }
    public Guid? GrantedProjectId { get; set; }
    public virtual Project GrantedProject { get; set; }
}
