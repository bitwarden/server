using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Policy : Core.Entities.Policy
{
    public virtual Organization Organization { get; set; }
}

public class PolicyMapperProfile : Profile
{
    public PolicyMapperProfile()
    {
        CreateMap<Core.Entities.Policy, Policy>().ReverseMap();
    }
}
