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
        // The read model speaks the wire's vocabulary: ResolvedDate is the stored ActionDate, and Status is derived
        // against the read clock by the repository (AccessStatusDerivation.ComputeStatus) after mapping -- the
        // stored action never leaves the repository on a read model.
        CreateMap<AccessRequest, Bit.Pam.Models.AccessRequestDetails>()
            .ForMember(d => d.ResolvedDate, opt => opt.MapFrom(src => src.ActionDate))
            .ForMember(d => d.Status, opt => opt.Ignore());
    }
}
