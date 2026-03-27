using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Vault.Models;

public sealed class UserPreferences : Core.Vault.Entities.UserPreferences
{
    public required User User { get; set; }
}

public class UserPreferencesMapperProfile : Profile
{
    public UserPreferencesMapperProfile()
    {
        CreateMap<Core.Vault.Entities.UserPreferences, UserPreferences>().ReverseMap();
    }
}
