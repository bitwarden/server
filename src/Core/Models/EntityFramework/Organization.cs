using System.Collections.Generic;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Organization : Table.Organization
    {
        public ICollection<Cipher> Ciphers { get; set; }
        public ICollection<OrganizationUser> OrganizationUsers { get; set; }
    }

    public class OrganizationMapperProfile : Profile
    {
        public OrganizationMapperProfile()
        {
            CreateMap<Table.Organization, Organization>().ReverseMap();
        }
    }
}
