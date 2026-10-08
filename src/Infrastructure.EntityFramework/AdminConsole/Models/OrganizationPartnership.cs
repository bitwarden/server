using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationPartnership : Core.AdminConsole.Entities.OrganizationPartnership
{
}

public class OrganizationPartnershipMapperProfile : Profile
{
    public OrganizationPartnershipMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationPartnership, OrganizationPartnership>()
            .ReverseMap();
    }
}
