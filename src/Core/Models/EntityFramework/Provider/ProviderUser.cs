using AutoMapper;

namespace Bit.Core.Models.EntityFramework.Provider
{
    public class ProviderUser : Table.Provider.ProviderUser
    {
        public virtual User User { get; set; }
        public virtual Provider Provider { get; set; }
    }

    public class ProviderUserMapperProfile : Profile
    {
        public ProviderUserMapperProfile()
        {
            CreateMap<Table.Provider.ProviderUser, ProviderUser>().ReverseMap();
        }
    }
}
