using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Device : Table.Device
    {
        public virtual User User { get; set; }
    }

    public class DeviceMapperProfile : Profile
    {
        public DeviceMapperProfile()
        {
            CreateMap<Table.Device, Device>().ReverseMap();
        }
    }
}
