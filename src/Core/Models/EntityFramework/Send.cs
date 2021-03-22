using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Send : Table.Send
    {
    }

    public class SendMapperProfile : Profile
    {
        public SendMapperProfile()
        {
            CreateMap<Table.Send, Send>().ReverseMap();
        }
    }
}
