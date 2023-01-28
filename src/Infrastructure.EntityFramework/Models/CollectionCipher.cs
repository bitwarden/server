using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class CollectionCipher : Core.Entities.CollectionCipher
{
    public virtual Cipher Cipher { get; set; }
    public virtual Collection Collection { get; set; }
}

public class CollectionCipherMapperProfile : Profile
{
    public CollectionCipherMapperProfile()
    {
        CreateMap<Core.Entities.CollectionCipher, CollectionCipher>().ReverseMap();
    }
}
