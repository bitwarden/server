using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;

public class OrganizationDeleteTask : Core.Dirt.Entities.OrganizationDeleteTask
{
}

public class OrganizationDeleteTaskProfile : Profile
{
    public OrganizationDeleteTaskProfile()
    {
        CreateMap<Core.Dirt.Entities.OrganizationDeleteTask, OrganizationDeleteTask>()
            .ReverseMap();
    }
}
