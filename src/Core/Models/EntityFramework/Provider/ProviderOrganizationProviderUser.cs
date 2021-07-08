using AutoMapper;

namespace Bit.Core.Models.EntityFramework.Provider
{
    public class ProviderOrganizationProviderUser : Table.Provider.ProviderOrganizationProviderUser
    {
        public virtual ProviderOrganization ProviderOrganization { get; set; }
        public virtual ProviderUser ProviderUser { get; set; }
    }

    public class ProviderOrganizationProviderUserMapperProfile : Profile
    {
        public ProviderOrganizationProviderUserMapperProfile()
        {
            CreateMap<Table.Provider.ProviderOrganizationProviderUser, ProviderOrganizationProviderUser>().ReverseMap();
        }
    }
}
