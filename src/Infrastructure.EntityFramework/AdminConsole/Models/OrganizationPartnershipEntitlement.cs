using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationPartnershipEntitlement : Core.AdminConsole.Entities.OrganizationPartnershipEntitlement
{
}

public class OrganizationPartnershipEntitlementMapperProfile : Profile
{
    public OrganizationPartnershipEntitlementMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationPartnershipEntitlement, OrganizationPartnershipEntitlement>()
            .ReverseMap();
    }
}
