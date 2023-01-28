using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class OrganizationSponsorship : Core.Entities.OrganizationSponsorship
{
    public virtual Organization SponsoringOrganization { get; set; }
    public virtual Organization SponsoredOrganization { get; set; }
}

public class OrganizationSponsorshipMapperProfile : Profile
{
    public OrganizationSponsorshipMapperProfile()
    {
        CreateMap<Core.Entities.OrganizationSponsorship, OrganizationSponsorship>().ReverseMap();
    }
}
