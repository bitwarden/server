using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Device : Table.Device
    {
    }

    public class DeviceMapperProfile : Profile
    {
        public DeviceMapperProfile()
        {
            CreateMap<Table.Device, Device>().ReverseMap();
        }
    }
}
