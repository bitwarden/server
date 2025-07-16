// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Platform;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

public class OrganizationInstallation : Core.Billing.Entities.OrganizationInstallation
{
    public virtual Installation Installation { get; set; }
    public virtual Organization Organization { get; set; }
}

public class OrganizationInstallationMapperProfile : Profile
{
    public OrganizationInstallationMapperProfile()
    {
        CreateMap<Core.Billing.Entities.OrganizationInstallation, OrganizationInstallation>().ReverseMap();
    }
}
