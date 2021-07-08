using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class CollectionCipher : Table.CollectionCipher
    {
        public virtual Cipher Cipher { get; set; }
        public virtual Collection Collection { get; set; }
    }

    public class CollectionCipherMapperProfile : Profile
    {
        public CollectionCipherMapperProfile()
        {
            CreateMap<Table.CollectionCipher, CollectionCipher>().ReverseMap();
        }
    }
}
