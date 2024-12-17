using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Installation : Core.Entities.Installation
{
    // Shadow property - to be introduced by https://bitwarden.atlassian.net/browse/PM-11129
    // This isn't a value or entity used by self hosted servers, but it's
    // being added for synchronicity between database provider options.
    public DateTime? LastActivityDate { get; set; }
}

public class InstallationMapperProfile : Profile
{
    public InstallationMapperProfile()
    {
        CreateMap<Core.Entities.Installation, Installation>()
            // Shadow property - to be introduced by https://bitwarden.atlassian.net/browse/PM-11129
            .ForMember(i => i.LastActivityDate, opt => opt.Ignore())
            .ReverseMap();
        CreateMap<Core.Entities.Installation, Installation>().ReverseMap();
    }
}
