using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationInviteLink : Core.AdminConsole.Entities.OrganizationInviteLink
{
    public virtual Organization Organization { get; set; } = null!;
}

public class OrganizationInviteLinkMapperProfile : Profile
{
    public OrganizationInviteLinkMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationInviteLink, OrganizationInviteLink>()
            .ReverseMap();
    }
}
