using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class TaxRate : Core.Models.Table.TaxRate
    {
    }

    public class TaxRateMapperProfile : Profile
    {
        public TaxRateMapperProfile()
        {
            CreateMap<Core.Models.Table.TaxRate, TaxRate>().ReverseMap();
        }
    }
}
