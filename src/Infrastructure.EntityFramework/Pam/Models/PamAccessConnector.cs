using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

public class PamAccessConnector : Bit.Pam.Entities.PamAccessConnector
{
    public virtual Organization? Organization { get; set; }
}

public class PamAccessConnectorMapperProfile : Profile
{
    public PamAccessConnectorMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.PamAccessConnector, PamAccessConnector>().ReverseMap();
    }
}
