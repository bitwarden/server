// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

public class AccessRequest : Bit.Pam.Entities.AccessRequest
{
    public virtual Organization Organization { get; set; }
}

public class AccessRequestMapperProfile : Profile
{
    public AccessRequestMapperProfile()
    {
        CreateMap<Bit.Pam.Entities.AccessRequest, AccessRequest>().ReverseMap();
        CreateMap<AccessRequest, Bit.Pam.Models.AccessRequestDetails>();
    }
}
