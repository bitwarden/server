using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Organization : Core.Entities.Organization
{
    public virtual ICollection<Cipher> Ciphers { get; set; }
    public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; }
    public virtual ICollection<Group> Groups { get; set; }
    public virtual ICollection<Policy> Policies { get; set; }
    public virtual ICollection<SsoConfig> SsoConfigs { get; set; }
    public virtual ICollection<SsoUser> SsoUsers { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
    public virtual ICollection<OrganizationApiKey> ApiKeys { get; set; }
    public virtual ICollection<OrganizationConnection> Connections { get; set; }
}

public class OrganizationMapperProfile : Profile
{
    public OrganizationMapperProfile()
    {
        CreateMap<Core.Entities.Organization, Organization>().ReverseMap();
    }
}
