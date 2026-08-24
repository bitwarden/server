using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamTargetSystem"/>, mirroring [dbo].[PamTargetSystem].
/// </summary>
public class PamTargetSystem : Bit.Pam.Entities.PamTargetSystem
{
    public virtual Organization? Organization { get; set; }
}

public class PamTargetSystemMapperProfile : Profile
{
    public PamTargetSystemMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamTargetSystem, PamTargetSystem>().ReverseMap();
    }
}
