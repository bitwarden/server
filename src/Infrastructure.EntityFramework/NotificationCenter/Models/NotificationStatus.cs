using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Models;

public class NotificationStatus : Core.NotificationCenter.Entities.NotificationStatus
{
    public virtual Notification Notification { get; set; }
    public virtual User User { get; set; }
}

public class NotificationStatusMapperProfile : Profile
{
    public NotificationStatusMapperProfile()
    {
        CreateMap<Core.NotificationCenter.Entities.NotificationStatus, NotificationStatus>()
            .PreserveReferences()
            .ReverseMap();
    }
}
