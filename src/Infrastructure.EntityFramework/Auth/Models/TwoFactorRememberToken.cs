using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class TwoFactorRememberToken : Core.Auth.Entities.TwoFactorRememberToken
{
    public virtual Bit.Infrastructure.EntityFramework.Models.User User { get; set; } = null!;
    public virtual Bit.Infrastructure.EntityFramework.Models.Device Device { get; set; } = null!;
}

public class TwoFactorRememberTokenMapperProfile : Profile
{
    public TwoFactorRememberTokenMapperProfile()
    {
        CreateMap<Core.Auth.Entities.TwoFactorRememberToken, TwoFactorRememberToken>()
            .ReverseMap();
    }
}
