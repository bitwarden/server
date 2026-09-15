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
        // ResolvedDate is the stored ActionDate; Status is derived against the read clock after mapping, so the
        // stored action never leaves the repository.
        CreateMap<AccessRequest, Bit.Pam.Models.AccessRequestDetails>()
            .ForMember(d => d.ResolvedDate, opt => opt.MapFrom(src => src.ActionDate))
            .ForMember(d => d.Status, opt => opt.Ignore());
    }
}
