using System.Collections.Generic;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Organization : Table.Organization
    {
        public virtual ICollection<Cipher> Ciphers { get; set; }
        public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; }
        public virtual ICollection<Group> Groups { get; set; }
        public virtual ICollection<Policy> Policies { get; set; }
        public virtual ICollection<SsoConfig> SsoConfigs { get; set; }
        public virtual ICollection<SsoUser> SsoUsers { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
    }

    public class OrganizationMapperProfile : Profile
    {
        public OrganizationMapperProfile()
        {
            CreateMap<Table.Organization, Organization>().ReverseMap();
        }
    }
}
