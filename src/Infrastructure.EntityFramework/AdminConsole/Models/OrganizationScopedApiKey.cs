using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationScopedApiKey : Core.AdminConsole.Entities.OrganizationScopedApiKey
{
    public virtual Organization Organization { get; set; } = null!;
}

public class OrganizationScopedApiKeyMapperProfile : Profile
{
    public OrganizationScopedApiKeyMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationScopedApiKey, OrganizationScopedApiKey>()
            .ReverseMap();
    }
}
