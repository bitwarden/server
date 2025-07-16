using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class OrganizationSubscriptionUpdate : Core.AdminConsole.Entities.OrganizationSubscriptionUpdate
{
    public virtual Organization? Organization { get; set; }
}

public class OrganizationSubscriptionUpdateMapperProfile : Profile
{
    public OrganizationSubscriptionUpdateMapperProfile()
    {
        CreateMap<Core.AdminConsole.Entities.OrganizationSubscriptionUpdate, OrganizationSubscriptionUpdate>().ReverseMap();
    }
}
