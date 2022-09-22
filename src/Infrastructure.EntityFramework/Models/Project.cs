using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Project : Core.Entities.Project
    {
        public virtual new ICollection<Secret> Secrets { get; set; }
        public virtual Organization Organization { get; set; }
    }

    public class ProjectMapperProfile : Profile
    {
        public ProjectMapperProfile()
        {
            CreateMap<Core.Entities.Project, Project>().PreserveReferences();
        }
    }
}
