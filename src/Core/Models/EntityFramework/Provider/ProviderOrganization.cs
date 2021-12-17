using AutoMapper;

namespace Bit.Core.Models.EntityFramework.Provider
{
    public class ProviderOrganization : Table.Provider.ProviderOrganization
    {
        public virtual Provider Provider { get; set; }
        public virtual Organization Organization { get; set; }
    }

    public class ProviderOrganizationMapperProfile : Profile
    {
        public ProviderOrganizationMapperProfile()
        {
            CreateMap<Table.Provider.ProviderOrganization, ProviderOrganization>().ReverseMap();
        }
    }
}
