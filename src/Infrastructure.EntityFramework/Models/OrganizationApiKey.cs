// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

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
