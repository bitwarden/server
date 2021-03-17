using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class SsoUser : Table.SsoUser
    {
        
    }

    public class SsoUserMapperProfile : Profile
    {
        public SsoUserMapperProfile()
        {
            CreateMap<Table.SsoUser, SsoUser>().ReverseMap();
        }
    }
}
