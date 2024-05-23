using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

public class ProviderPlan : Core.Billing.Entities.ProviderPlan
{
    public virtual Provider Provider { get; set; }
}

public class ProviderPlanMapperProfile : Profile
{
    public ProviderPlanMapperProfile()
    {
        CreateMap<Core.Billing.Entities.ProviderPlan, ProviderPlan>().ReverseMap();
    }
}
