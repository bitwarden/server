using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class AuthRequest : Core.Entities.AuthRequest
{
    public virtual User User { get; set; }
    public virtual Device ResponseDevice { get; set; }
}

public class AuthRequestMapperProfile : Profile
{
    public AuthRequestMapperProfile()
    {
        CreateMap<Core.Entities.AuthRequest, AuthRequest>().ReverseMap();
    }
}
