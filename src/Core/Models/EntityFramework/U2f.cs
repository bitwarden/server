using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class U2f : Table.U2f
    {
        public virtual User User { get; set; }
    }

    public class U2fMapperProfile : Profile
    {
        public U2fMapperProfile()
        {
            CreateMap<Table.U2f, U2f>().ReverseMap();
        }
    }
}
