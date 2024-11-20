using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Models;

public class Notification : Core.NotificationCenter.Entities.Notification
{
    public virtual User User { get; set; }
    public virtual Organization Organization { get; set; }
}

public class NotificationMapperProfile : Profile
{
    public NotificationMapperProfile()
    {
        CreateMap<Core.NotificationCenter.Entities.Notification, Notification>()
            .PreserveReferences()
            .ReverseMap();
    }
}
