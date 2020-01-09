using System.Collections.Generic;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class User : Table.User
    {
        public ICollection<Cipher> Ciphers { get; set; }
    }

    public class UserMapperProfile : Profile
    {
        public UserMapperProfile()
        {
            CreateMap<Table.User, User>().ReverseMap();
        }
    }
}
