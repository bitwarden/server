using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;
public class RiskInsightCriticalApplication : Core.Dirt.Reports.Entities.RiskInsightCriticalApplication
{
    public virtual Organization Organization { get; set; }
}

public class RiskInsightCriticalApplicationProfile : Profile
{
    public RiskInsightCriticalApplicationProfile()
    {
        CreateMap<Core.Dirt.Reports.Entities.RiskInsightCriticalApplication, RiskInsightCriticalApplication>()
            .ReverseMap();
    }
}
