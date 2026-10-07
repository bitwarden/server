using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

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
