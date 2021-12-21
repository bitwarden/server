using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Folder : Table.Folder
    {
        public virtual User User { get; set; }
    }

    public class FolderMapperProfile : Profile
    {
        public FolderMapperProfile()
        {
            CreateMap<Table.Folder, Folder>().ReverseMap();
        }
    }
}
