using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class User : Table.User
    {
        private JsonDocument _twoFactorProvidersJson;

        public ICollection<Cipher> Ciphers { get; set; }

        public JsonDocument TwoFactorProvidersJson
        {
            get => _twoFactorProvidersJson;
            set
            {
                TwoFactorProviders = value.ToString();
                _twoFactorProvidersJson = value;
            }
        }
    }

    public class UserMapperProfile : Profile
    {
        public UserMapperProfile()
        {
            CreateMap<Table.User, User>().ReverseMap();
        }
    }
}
