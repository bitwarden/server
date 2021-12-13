using System.Collections.Generic;
using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class User : Core.Models.Table.User
    {
        public virtual ICollection<Cipher> Ciphers { get; set; }
        public virtual ICollection<Folder> Folders { get; set; }
        public virtual ICollection<CollectionUser> CollectionUsers { get; set; }
        public virtual ICollection<GroupUser> GroupUsers { get; set; }
        public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; }
        public virtual ICollection<SsoUser> SsoUsers { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
        public virtual ICollection<U2f> U2fs { get; set; }
    }

    public class UserMapperProfile : Profile
    {
        public UserMapperProfile()
        {
            CreateMap<Core.Models.Table.User, User>().ReverseMap();
        }
    }
}
