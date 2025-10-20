#nullable enable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class SecretVersion : Core.SecretsManager.Entities.SecretVersion
{
    public Secret? Secret { get; set; }

    public ServiceAccount? EditorServiceAccount { get; set; }

    public Bit.Infrastructure.EntityFramework.Models.OrganizationUser? EditorOrganizationUser { get; set; }
}

public class SecretVersionMapperProfile : Profile
{
    public SecretVersionMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.SecretVersion, SecretVersion>()
            .PreserveReferences()
            .ReverseMap();
    }
}
