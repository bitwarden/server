using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Grant : Table.Grant
    {
    }

    public class GrantMapperProfile : Profile
    {
        public GrantMapperProfile()
        {
            CreateMap<Table.Grant, Grant>().ReverseMap();
        }
    }
}
