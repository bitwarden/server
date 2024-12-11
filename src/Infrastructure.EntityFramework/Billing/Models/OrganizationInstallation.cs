using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;

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
