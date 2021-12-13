using System.Collections.Generic;
using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class OrganizationUser : Core.Models.Table.OrganizationUser
    {
        public virtual Organization Organization { get; set; }
        public virtual User User { get; set; }
        public virtual ICollection<CollectionUser> CollectionUsers { get; set; }
    }

    public class OrganizationUserMapperProfile : Profile
    {
        public OrganizationUserMapperProfile()
        {
            CreateMap<Core.Models.Table.OrganizationUser, OrganizationUser>().ReverseMap();
        }
    }
}
