using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class OrganizationApiKey : Core.Entities.OrganizationApiKey
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationApiKeyMapperProfile : Profile
{
    public OrganizationApiKeyMapperProfile()
    {
        CreateMap<Core.Entities.OrganizationApiKey, OrganizationApiKey>().ReverseMap();
    }
}
