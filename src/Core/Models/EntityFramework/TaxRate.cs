using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class TaxRate : Table.TaxRate
    {
    }

    public class TaxRateMapperProfile : Profile
    {
        public TaxRateMapperProfile()
        {
            CreateMap<Table.TaxRate, TaxRate>().ReverseMap();
        }
    }
}
