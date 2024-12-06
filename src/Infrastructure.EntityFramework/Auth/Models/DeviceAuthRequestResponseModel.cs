using AutoMapper;
using Bit.Core.Auth.Models.Api.Response;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class DeviceAuthRequestResponseModelMapperProfile : Profile
{
    public DeviceAuthRequestResponseModelMapperProfile()
    {
        CreateMap<Core.Entities.Device, DeviceAuthRequestResponseModel>().ReverseMap();
    }
}
