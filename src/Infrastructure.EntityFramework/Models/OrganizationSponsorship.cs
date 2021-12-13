using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class OrganizationSponsorship : Core.Models.Table.OrganizationSponsorship
    {
        public virtual Installation Installation { get; set; }
        public virtual Organization SponsoringOrganization { get; set; }
        public virtual Organization SponsoredOrganization { get; set; }
    }

    public class OrganizationSponsorshipMapperProfile : Profile
    {
        public OrganizationSponsorshipMapperProfile()
        {
            CreateMap<Core.Models.Table.OrganizationSponsorship, OrganizationSponsorship>().ReverseMap();
        }
    }
}
