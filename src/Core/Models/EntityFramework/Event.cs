using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Event : Table.Event
    {
    }

    public class EventMapperProfile : Profile
    {
        public EventMapperProfile()
        {
            CreateMap<Table.Event, Event>().ReverseMap();
        }
    }
}
