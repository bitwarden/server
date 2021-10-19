using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Cipher : Table.Cipher
    {
        public virtual User User { get; set; }
        public virtual Organization Organization { get; set; }
        public virtual ICollection<CollectionCipher> CollectionCiphers { get; set; }
    }

    public class CipherMapperProfile : Profile
    {
        public CipherMapperProfile()
        {
            CreateMap<Table.Cipher, Cipher>().ReverseMap();
        }
    }
}
