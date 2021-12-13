using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Policy : Core.Models.Table.Policy
    {
        public virtual Organization Organization { get; set; }
    }

    public class PolicyMapperProfile : Profile
    {
        public PolicyMapperProfile()
        {
            CreateMap<Core.Models.Table.Policy, Policy>().ReverseMap();
        }
    }
}
