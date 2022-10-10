using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class ApiKey : Core.Entities.ApiKey
{
    public virtual ServiceAccount ServiceAccount { get; set; }
}

public class ApiKeyMapperProfile : Profile
{
    public ApiKeyMapperProfile()
    {
        CreateMap<Core.Entities.ApiKey, ApiKey>().ReverseMap();
    }
}
