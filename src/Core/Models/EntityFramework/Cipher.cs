using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Cipher : Table.Cipher
    {
        public User User { get; set; }
        public Organization Organization { get; set; }
    }

    public class CipherMapperProfile : Profile
    {
        public CipherMapperProfile()
        {
            CreateMap<Table.Cipher, Cipher>().ReverseMap();
        }
    }
}
