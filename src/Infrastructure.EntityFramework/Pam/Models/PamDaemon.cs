using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamDaemon"/>, mirroring [dbo].[PamDaemon].
/// </summary>
public class PamDaemon : Bit.Pam.Entities.PamDaemon
{
    public virtual Organization? Organization { get; set; }
}

public class PamDaemonMapperProfile : Profile
{
    public PamDaemonMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamDaemon, PamDaemon>().ReverseMap();
    }
}
