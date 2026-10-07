using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

public class PamRotationConfig : Bit.Pam.Entities.PamRotationConfig
{
    public virtual Organization? Organization { get; set; }
}

public class PamRotationConfigMapperProfile : Profile
{
    public PamRotationConfigMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamRotationConfig, PamRotationConfig>().ReverseMap();
    }
}
