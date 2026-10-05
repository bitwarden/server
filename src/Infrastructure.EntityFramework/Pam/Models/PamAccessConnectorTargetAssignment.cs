using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamAccessConnectorTargetAssignment"/>, mirroring
/// [dbo].[PamAccessConnectorTargetAssignment].
/// </summary>
public class PamAccessConnectorTargetAssignment : Bit.Pam.Entities.PamAccessConnectorTargetAssignment
{
    public virtual Organization? Organization { get; set; }
}

public class PamAccessConnectorTargetAssignmentMapperProfile : Profile
{
    public PamAccessConnectorTargetAssignmentMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamAccessConnectorTargetAssignment, PamAccessConnectorTargetAssignment>()
            .ReverseMap();
    }
}
