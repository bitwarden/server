using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;
public class RiskInsightReport : Core.Dirt.Reports.Entities.RiskInsightReport
{
    public virtual Organization Organization { get; set; }
}

public class RiskInsightReportProfile : Profile
{
    public RiskInsightReportProfile()
    {
        CreateMap<Core.Dirt.Reports.Entities.RiskInsightReport, RiskInsightReport>()
            .ReverseMap();
    }
}
