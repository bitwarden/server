using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

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
