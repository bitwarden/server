using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Secret : Core.Entities.Secret
{
    public virtual new ICollection<Project> Projects { get; set; }
    public virtual Organization Organization { get; set; }
}

public class SecretMapperProfile : Profile
{
    public SecretMapperProfile()
    {
        CreateMap<Core.Entities.Secret, Secret>()
            .PreserveReferences()
            .ReverseMap();
    }
}
