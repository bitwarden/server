using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamDaemonTargetAssignment"/>, mirroring [dbo].[PamDaemonTargetAssignment].
/// </summary>
public class PamDaemonTargetAssignment : Bit.Pam.Entities.PamDaemonTargetAssignment
{
    public virtual Organization? Organization { get; set; }
}

public class PamDaemonTargetAssignmentMapperProfile : Profile
{
    public PamDaemonTargetAssignmentMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamDaemonTargetAssignment, PamDaemonTargetAssignment>().ReverseMap();
    }
}
