using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Collection : Core.Entities.Collection
{
    public virtual Organization Organization { get; set; }
    public virtual ICollection<CollectionUser> CollectionUsers { get; set; }
    public virtual ICollection<CollectionCipher> CollectionCiphers { get; set; }
    public virtual ICollection<CollectionGroup> CollectionGroups { get; set; }
}

public class CollectionMapperProfile : Profile
{
    public CollectionMapperProfile()
    {
        CreateMap<Core.Entities.Collection, Collection>().ReverseMap();
    }
}
