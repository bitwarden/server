using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Project : Core.Entities.Project
    {
    }

    public class ProjectMapperProfile : Profile
    {
        public ProjectMapperProfile()
        {
            CreateMap<Core.Entities.Project, Project>().ReverseMap();
        }
    }
}
