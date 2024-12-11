using AutoMapper;
using Bit.Core.Auth.Models.Data;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class AuthRequest : Core.Auth.Entities.AuthRequest
{
    public virtual User User { get; set; }
    public virtual Device ResponseDevice { get; set; }
    public virtual Organization Organization { get; set; }
}

public class AuthRequestMapperProfile : Profile
{
    public AuthRequestMapperProfile()
    {
        CreateMap<Core.Auth.Entities.AuthRequest, AuthRequest>().ReverseMap();
        CreateProjection<AuthRequest, OrganizationAdminAuthRequest>()
            .ForMember(m => m.Email, opt => opt.MapFrom(t => t.User.Email))
            .ForMember(
                m => m.OrganizationUserId,
                opt =>
                    opt.MapFrom(t =>
                        t.User.OrganizationUsers.FirstOrDefault(ou =>
                            ou.OrganizationId == t.OrganizationId && ou.UserId == t.UserId
                        ).Id
                    )
            );
    }
}
