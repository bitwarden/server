using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class OrganizationUser : Table.OrganizationUser
    {
        public Organization Organization { get; set; }
        public User User { get; set; }
    }

    public class OrganizationUserMapperProfile : Profile
    {
        public OrganizationUserMapperProfile()
        {
            CreateMap<Table.OrganizationUser, OrganizationUser>().ReverseMap();
        }
    }
}
