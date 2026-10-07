using AutoMapper;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

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
