using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationApiKey : Core.AdminConsole.Entities.OrganizationApiKey
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationApiKeyMapperProfile : Profile
{
    public OrganizationApiKeyMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationApiKey, OrganizationApiKey>().ReverseMap();
    }
}
