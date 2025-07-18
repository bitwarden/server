﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Send : Core.Tools.Entities.Send
{
    public virtual Organization Organization { get; set; }
    public virtual User User { get; set; }
}

public class SendMapperProfile : Profile
{
    public SendMapperProfile()
    {
        CreateMap<Core.Tools.Entities.Send, Send>().ReverseMap();
    }
}
