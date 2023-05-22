using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Vault.Models;

public class Cipher : Core.Vault.Entities.Cipher
{
    public virtual User User { get; set; }
    public virtual Organization Organization { get; set; }
    public virtual ICollection<CollectionCipher> CollectionCiphers { get; set; }
}

public class CipherMapperProfile : Profile
{
    public CipherMapperProfile()
    {
        CreateMap<Core.Vault.Entities.Cipher, Cipher>().ReverseMap();
    }
}
