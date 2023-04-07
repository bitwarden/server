using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Grant : Core.Auth.Entities.Grant
{
}

public class GrantMapperProfile : Profile
{
    public GrantMapperProfile()
    {
        CreateMap<Core.Auth.Entities.Grant, Grant>().ReverseMap();
    }
}
