using AutoMapper;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for <see cref="Bit.Pam.Entities.PamRotationAttempt"/>, mirroring [dbo].[PamRotationAttempt].
/// </summary>
public class PamRotationAttempt : Bit.Pam.Entities.PamRotationAttempt
{
}

public class PamRotationAttemptMapperProfile : Profile
{
    public PamRotationAttemptMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamRotationAttempt, PamRotationAttempt>().ReverseMap();
    }
}
