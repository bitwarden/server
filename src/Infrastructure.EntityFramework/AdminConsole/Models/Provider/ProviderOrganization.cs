using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

public class ProviderOrganization : Core.AdminConsole.Entities.Provider.ProviderOrganization
{
    public virtual Provider Provider { get; set; }
    public virtual Organization Organization { get; set; }
}

public class ProviderOrganizationMapperProfile : Profile
{
    public ProviderOrganizationMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.Provider.ProviderOrganization, ProviderOrganization>().ReverseMap();
    }
}
