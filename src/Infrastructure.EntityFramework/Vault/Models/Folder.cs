using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Folder : Core.Entities.Folder
{
    public virtual User User { get; set; }
}

public class FolderMapperProfile : Profile
{
    public FolderMapperProfile()
    {
        CreateMap<Core.Entities.Folder, Folder>().ReverseMap();
    }
}
