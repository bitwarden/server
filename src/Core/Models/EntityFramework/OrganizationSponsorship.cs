using System.Collections.Generic;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class OrganizationSponsorship : Table.OrganizationSponsorship
    {
        public virtual Installation Installation { get; set; }
        public virtual Organization SponsoringOrganization { get; set; }
        public virtual Organization SponsoredOrganization { get; set; }
    }

    public class OrganizationSponsorshipMapperProfile : Profile
    {
        public OrganizationSponsorshipMapperProfile()
        {
            CreateMap<Table.OrganizationSponsorship, OrganizationSponsorship>().ReverseMap();
        }
    }
}
