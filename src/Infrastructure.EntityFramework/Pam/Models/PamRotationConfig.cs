using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamRotationConfig"/>, mirroring [dbo].[PamRotationConfig].
/// </summary>
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
