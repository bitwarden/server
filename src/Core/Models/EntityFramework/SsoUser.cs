using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class SsoUser : Table.SsoUser
    {
        public virtual Organization Organization { get; set; }
        public virtual User User { get; set; }
    }

    public class SsoUserMapperProfile : Profile
    {
        public SsoUserMapperProfile()
        {
            CreateMap<Table.SsoUser, SsoUser>().ReverseMap();
        }
    }
}
