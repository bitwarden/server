using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class ApiKey : Core.SecretsManager.Entities.ApiKey
{
    public virtual ServiceAccount ServiceAccount { get; set; }
}

public class ApiKeyMapperProfile : Profile
{
    public ApiKeyMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.ApiKey, ApiKey>().ReverseMap();
    }
}
