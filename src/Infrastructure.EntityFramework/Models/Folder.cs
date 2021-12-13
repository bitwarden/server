using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Folder : Core.Models.Table.Folder
    {
        public virtual User User { get; set; }
    }

    public class FolderMapperProfile : Profile
    {
        public FolderMapperProfile()
        {
            CreateMap<Core.Models.Table.Folder, Folder>().ReverseMap();
        }
    }
}
