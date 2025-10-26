// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Vault.Models;

public class CipherHistory : Core.Vault.Entities.CipherHistory
{
    public virtual Cipher Cipher { get; set; }
}

public class CipherHistoryMapperProfile : Profile
{
    public CipherHistoryMapperProfile()
    {
        CreateMap<Core.Vault.Entities.CipherHistory, CipherHistory>().ReverseMap();
    }
}
