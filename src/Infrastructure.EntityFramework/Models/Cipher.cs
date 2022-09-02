using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Cipher : Core.Entities.Cipher
{
    public virtual User User { get; set; }
    public virtual Organization Organization { get; set; }
    public virtual ICollection<CollectionCipher> CollectionCiphers { get; set; }
}

public class CipherMapperProfile : Profile
{
    public CipherMapperProfile()
    {
        CreateMap<Core.Entities.Cipher, Cipher>().ReverseMap();
    }
}
