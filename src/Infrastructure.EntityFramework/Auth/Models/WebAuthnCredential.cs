﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Auth.Models;

public class WebAuthnCredential : Core.Auth.Entities.WebAuthnCredential
{
    public virtual User User { get; set; }
}

public class WebAuthnCredentialMapperProfile : Profile
{
    public WebAuthnCredentialMapperProfile()
    {
        CreateMap<Core.Auth.Entities.WebAuthnCredential, WebAuthnCredential>().ReverseMap();
    }
}
