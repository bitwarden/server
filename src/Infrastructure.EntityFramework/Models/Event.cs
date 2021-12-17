using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Event : Core.Models.Table.Event
    {
    }

    public class EventMapperProfile : Profile
    {
        public EventMapperProfile()
        {
            CreateMap<Core.Models.Table.Event, Event>().ReverseMap();
        }
    }
}
