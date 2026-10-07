using AutoMapper;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

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
