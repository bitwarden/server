using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Policy : Table.Policy
    {
        public virtual Organization Organization { get; set; }
    }

    public class PolicyMapperProfile : Profile
    {
        public PolicyMapperProfile()
        {
            CreateMap<Table.Policy, Policy>().ReverseMap();
        }
    }
}
