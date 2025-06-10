using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;
public class OrganizationReport : Core.Dirt.Reports.Entities.OrganizationReport
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationReportProfile : Profile
{
    public OrganizationReportProfile()
    {
        CreateMap<Core.Dirt.Reports.Entities.OrganizationReport, OrganizationReport>()
            .ReverseMap();
    }
}
