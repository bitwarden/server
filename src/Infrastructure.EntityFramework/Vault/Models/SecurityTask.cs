// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Vault.Models;

public class SecurityTask : Core.Vault.Entities.SecurityTask
{
    public virtual Organization Organization { get; set; }
    public virtual Cipher Cipher { get; set; }
}

public class SecurityTaskMapperProfile : Profile
{
    public SecurityTaskMapperProfile()
    {
        CreateMap<Core.Vault.Entities.SecurityTask, SecurityTask>().ReverseMap();
    }
}
