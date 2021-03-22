using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Policy : Table.Policy
    {
    }

    public class PolicyMapperProfile : Profile
    {
        public PolicyMapperProfile()
        {
            CreateMap<Table.Policy, Policy>().ReverseMap();
        }
    }
}
