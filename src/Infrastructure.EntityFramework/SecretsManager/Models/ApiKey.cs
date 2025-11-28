// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
