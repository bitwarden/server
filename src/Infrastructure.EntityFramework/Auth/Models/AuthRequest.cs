using AutoMapper;
using Bit.Core.Auth.Models.Data;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class AuthRequest : Core.Auth.Entities.AuthRequest
{
    public virtual User User { get; set; }
    public virtual Device ResponseDevice { get; set; }
}

public class AuthRequestMapperProfile : Profile
{
    public AuthRequestMapperProfile()
    {
        CreateMap<Core.Auth.Entities.AuthRequest, AuthRequest>().ReverseMap();
        CreateMap<Core.Auth.Entities.AuthRequest, OrganizationAdminAuthRequest>();
    }
}
