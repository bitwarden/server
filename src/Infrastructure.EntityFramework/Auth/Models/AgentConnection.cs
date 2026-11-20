// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class AgentConnection : Core.Auth.Entities.AgentConnection
{
    public virtual User User { get; set; }
}

public class AgentConnectionMapperProfile : Profile
{
    public AgentConnectionMapperProfile()
    {
        CreateMap<Core.Auth.Entities.AgentConnection, AgentConnection>().ReverseMap();
    }
}
