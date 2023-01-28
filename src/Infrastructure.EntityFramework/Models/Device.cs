using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Device : Core.Entities.Device
{
    public virtual User User { get; set; }
}

public class DeviceMapperProfile : Profile
{
    public DeviceMapperProfile()
    {
        CreateMap<Core.Entities.Device, Device>().ReverseMap();
    }
}
