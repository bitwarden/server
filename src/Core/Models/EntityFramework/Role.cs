using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Role : Table.Role
    {
    }

    public class RoleMapperProfile : Profile
    {
        public RoleMapperProfile()
        {
            CreateMap<Table.Role, Role>().ReverseMap();
        }
    }
}
