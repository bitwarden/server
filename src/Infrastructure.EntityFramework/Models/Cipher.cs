using System.Collections.Generic;
using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Cipher : Core.Models.Table.Cipher
    {
        public virtual User User { get; set; }
        public virtual Organization Organization { get; set; }
        public virtual ICollection<CollectionCipher> CollectionCiphers { get; set; }
    }

    public class CipherMapperProfile : Profile
    {
        public CipherMapperProfile()
        {
            CreateMap<Core.Models.Table.Cipher, Cipher>().ReverseMap();
        }
    }
}
