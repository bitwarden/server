// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
