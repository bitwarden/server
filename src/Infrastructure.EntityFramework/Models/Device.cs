using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Device : Core.Models.Table.Device
    {
        public virtual User User { get; set; }
    }

    public class DeviceMapperProfile : Profile
    {
        public DeviceMapperProfile()
        {
            CreateMap<Core.Models.Table.Device, Device>().ReverseMap();
        }
    }
}
