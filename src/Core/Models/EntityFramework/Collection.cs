using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Collection : Table.Collection
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
            CreateMap<Table.Collection, Collection>().ReverseMap();
        }
    }
}
