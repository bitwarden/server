using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class TaxRate : Core.Entities.TaxRate { }

public class TaxRateMapperProfile : Profile
{
    public TaxRateMapperProfile()
    {
        CreateMap<Core.Entities.TaxRate, TaxRate>().ReverseMap();
    }
}
