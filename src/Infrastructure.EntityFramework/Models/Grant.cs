using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Grant : Core.Entities.Grant
{
}

public class GrantMapperProfile : Profile
{
    public GrantMapperProfile()
    {
        CreateMap<Core.Entities.Grant, Grant>().ReverseMap();
    }
}
