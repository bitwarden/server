﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Models;

public class OrganizationUser : Core.Entities.OrganizationUser
{
    public virtual Organization Organization { get; set; }
    public virtual User User { get; set; }
    public virtual ICollection<CollectionUser> CollectionUsers { get; set; }
    public virtual ICollection<GroupUser> GroupUsers { get; set; }
}

public class OrganizationUserMapperProfile : Profile
{
    public OrganizationUserMapperProfile()
    {
        CreateMap<Core.Entities.OrganizationUser, OrganizationUser>().ReverseMap();
    }
}
