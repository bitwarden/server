using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Collection : Table.Collection
    {
        List<CollectionUser> CollectionUsers { get; set; }
    }

    public class CollectionMapperProfile : Profile
    {
        public CollectionMapperProfile()
        {
            CreateMap<Table.Collection, Collection>().ReverseMap();
        }
    }
}
