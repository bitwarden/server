using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class Policy : Core.AdminConsole.Entities.Policy
{
    public virtual Organization Organization { get; set; }
}

public class PolicyMapperProfile : Profile
{
    public PolicyMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.Policy, Policy>().ReverseMap();
    }
}
