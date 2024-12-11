using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Event : Core.Entities.Event { }

public class EventMapperProfile : Profile
{
    public EventMapperProfile()
    {
        CreateMap<Core.Entities.Event, Event>().ReverseMap();
    }
}
