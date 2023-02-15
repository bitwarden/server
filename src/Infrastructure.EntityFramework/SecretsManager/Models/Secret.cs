using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class Secret : Core.SecretsManager.Entities.Secret
{
    public virtual new ICollection<Project> Projects { get; set; }
    public virtual Organization Organization { get; set; }
}

public class SecretMapperProfile : Profile
{
    public SecretMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.Secret, Secret>()
            .PreserveReferences()
            .ReverseMap();
    }
}
