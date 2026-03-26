using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Tools.Models;

public class Receive : Core.Tools.Entities.Receive
{
    public virtual User User { get; set; } = null!;
}

public class ReceiveMapperProfile : Profile
{
    public ReceiveMapperProfile()
    {
        CreateMap<Core.Tools.Entities.Receive, Receive>().ReverseMap();
    }
}
