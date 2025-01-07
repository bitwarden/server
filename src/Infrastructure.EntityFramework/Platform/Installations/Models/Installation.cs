using AutoMapper;
using C = Bit.Core.Platform.Installations;

namespace Bit.Infrastructure.EntityFramework.Platform;

public class Installation : C.Installation
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
        CreateMap<C.Installation, Installation>()
            // Shadow property - to be introduced by https://bitwarden.atlassian.net/browse/PM-11129
            .ForMember(i => i.LastActivityDate, opt => opt.Ignore())
            .ReverseMap();
        CreateMap<C.Installation, Installation>().ReverseMap();
    }
}
