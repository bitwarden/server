#nullable enable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class PlayItem : Core.Entities.PlayItem
{
    public virtual User? User { get; set; }
    public virtual AdminConsole.Models.Organization? Organization { get; set; }
}

public class PlayItemMapperProfile : Profile
{
    public PlayItemMapperProfile()
    {
        CreateMap<Core.Entities.PlayItem, PlayItem>().ReverseMap();
    }
}
