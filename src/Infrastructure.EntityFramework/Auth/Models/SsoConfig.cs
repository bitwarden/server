﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class SsoConfig : Core.Auth.Entities.SsoConfig
{
    public virtual Organization Organization { get; set; }
}

public class SsoConfigMapperProfile : Profile
{
    public SsoConfigMapperProfile()
    {
        CreateMap<Core.Auth.Entities.SsoConfig, SsoConfig>().ReverseMap();
    }
}
