using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Role : Core.Entities.Role { }

public class RoleMapperProfile : Profile
{
    public RoleMapperProfile()
    {
        CreateMap<Core.Entities.Role, Role>().ReverseMap();
    }
}
