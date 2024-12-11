using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class Grant : Core.Auth.Entities.Grant { }

public class GrantMapperProfile : Profile
{
    public GrantMapperProfile()
    {
        CreateMap<Core.Auth.Entities.Grant, Grant>().ReverseMap();
    }
}
