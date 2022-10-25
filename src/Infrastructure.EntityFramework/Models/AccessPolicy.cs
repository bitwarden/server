using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class AccessPolicy : Core.Entities.AccessPolicy
{
    public virtual OrganizationUser OrganizationUser { get; set; }
    public virtual Group Group { get; set; }
    public virtual ServiceAccount ServiceAccount { get; set; }

    public virtual Project Project { get; set; }
    public virtual Secret Secret { get; set; }
}

public class AccessPolicyMapperProfile : Profile
{
    public AccessPolicyMapperProfile()
    {
        CreateMap<Core.Entities.AccessPolicy, AccessPolicy>().ReverseMap();
    }
}
