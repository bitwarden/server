using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class CollectionCipher : Core.Models.Table.CollectionCipher
    {
        public virtual Cipher Cipher { get; set; }
        public virtual Collection Collection { get; set; }
    }

    public class CollectionCipherMapperProfile : Profile
    {
        public CollectionCipherMapperProfile()
        {
            CreateMap<Core.Models.Table.CollectionCipher, CollectionCipher>().ReverseMap();
        }
    }
}
