using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Role : Core.Models.Table.Role
    {
    }

    public class RoleMapperProfile : Profile
    {
        public RoleMapperProfile()
        {
            CreateMap<Core.Models.Table.Role, Role>().ReverseMap();
        }
    }
}
