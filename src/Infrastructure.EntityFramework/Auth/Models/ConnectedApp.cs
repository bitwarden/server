// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class ConnectedApp : Core.Auth.Entities.ConnectedApp
{
    public virtual User User { get; set; }
}

public class ConnectedAppMapperProfile : Profile
{
    public ConnectedAppMapperProfile()
    {
        CreateMap<Core.Auth.Entities.ConnectedApp, ConnectedApp>().ReverseMap();
    }
}
