// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;
public class OrganizationReport : Core.Dirt.Entities.OrganizationReport
{
    public virtual Organization Organization { get; set; }
}

public class OrganizationReportProfile : Profile
{
    public OrganizationReportProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationReport, OrganizationReport>()
            .ReverseMap();
    }
}
