using AutoMapper;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamRotationJob"/>, mirroring [dbo].[PamRotationJob].
/// </summary>
public class PamRotationJob : Bit.Pam.Entities.PamRotationJob
{
}

public class PamRotationJobMapperProfile : Profile
{
    public PamRotationJobMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamRotationJob, PamRotationJob>().ReverseMap();
    }
}
