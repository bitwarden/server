using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Send : Table.Send
    {
        public virtual Organization Organization { get; set; }
        public virtual User User { get; set; }
    }

    public class SendMapperProfile : Profile
    {
        public SendMapperProfile()
        {
            CreateMap<Table.Send, Send>().ReverseMap();
        }
    }
}
