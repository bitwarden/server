#nullable enable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class PlayData : Core.Entities.PlayData
{
    public virtual User? User { get; set; }
    public virtual AdminConsole.Models.Organization? Organization { get; set; }
}

public class PlayDataMapperProfile : Profile
{
    public PlayDataMapperProfile()
    {
        CreateMap<Core.Entities.PlayData, PlayData>().ReverseMap();
    }
}
