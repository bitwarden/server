using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class ProviderOrganization : Core.Models.Table.Provider.ProviderOrganization
    {
        public virtual Provider Provider { get; set; }
        public virtual Organization Organization { get; set; }
    }

    public class ProviderOrganizationMapperProfile : Profile
    {
        public ProviderOrganizationMapperProfile()
        {
            CreateMap<Core.Models.Table.Provider.ProviderOrganization, ProviderOrganization>().ReverseMap();
        }
    }
}
