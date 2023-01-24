using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class ServiceAccount : Core.Entities.ServiceAccount
{
    public virtual Organization Organization { get; set; }
    public virtual ICollection<GroupServiceAccountAccessPolicy> GroupAccessPolicies { get; set; }
    public virtual ICollection<UserServiceAccountAccessPolicy> UserAccessPolicies { get; set; }
}

public class ServiceAccountMapperProfile : Profile
{
    public ServiceAccountMapperProfile()
    {
        CreateMap<Core.Entities.ServiceAccount, ServiceAccount>().ReverseMap();
    }
}
