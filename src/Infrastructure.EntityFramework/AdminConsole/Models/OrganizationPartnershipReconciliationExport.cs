using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationPartnershipReconciliationExport
    : Core.AdminConsole.Entities.OrganizationPartnershipReconciliationExport
{
}

public class OrganizationPartnershipReconciliationExportMapperProfile : Profile
{
    public OrganizationPartnershipReconciliationExportMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationPartnershipReconciliationExport,
                OrganizationPartnershipReconciliationExport>()
            .ReverseMap();
    }
}
