// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;

public class OrganizationReportSummary : Core.Dirt.Entities.OrganizationReportSummary
{
    public virtual OrganizationReport OrganizationReport { get; set; }
}

public class OrganizationReportSummaryProfile : Profile
{
    public OrganizationReportSummaryProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationReportSummary, OrganizationReportSummary>()
            .ReverseMap();
    }
}
