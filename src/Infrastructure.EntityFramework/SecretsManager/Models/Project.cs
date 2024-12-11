﻿using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class Project : Core.SecretsManager.Entities.Project
{
    public new virtual ICollection<Secret> Secrets { get; set; }
    public virtual Organization Organization { get; set; }
    public virtual ICollection<GroupProjectAccessPolicy> GroupAccessPolicies { get; set; }
    public virtual ICollection<UserProjectAccessPolicy> UserAccessPolicies { get; set; }
    public virtual ICollection<ServiceAccountProjectAccessPolicy> ServiceAccountAccessPolicies { get; set; }
}

public class ProjectMapperProfile : Profile
{
    public ProjectMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.Project, Project>()
            .PreserveReferences()
            .ReverseMap();
    }
}
