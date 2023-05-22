using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Vault.Models;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Organization : Core.Entities.Organization
{
    public virtual ICollection<Cipher> Ciphers { get; set; }
    public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; }
    public virtual ICollection<Group> Groups { get; set; }
    public virtual ICollection<Policy> Policies { get; set; }
    public virtual ICollection<Collection> Collections { get; set; }
    public virtual ICollection<SsoConfig> SsoConfigs { get; set; }
    public virtual ICollection<SsoUser> SsoUsers { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
    public virtual ICollection<OrganizationApiKey> ApiKeys { get; set; }
    public virtual ICollection<OrganizationConnection> Connections { get; set; }
    public virtual ICollection<OrganizationDomain> Domains { get; set; }
}

public class OrganizationMapperProfile : Profile
{
    public OrganizationMapperProfile()
    {
        CreateMap<Core.Entities.Organization, Organization>().ReverseMap();
        CreateProjection<Organization, SelfHostedOrganizationDetails>()
            .ForMember(sd => sd.CollectionCount, opt => opt.MapFrom(o => o.Collections.Count))
            .ForMember(sd => sd.GroupCount, opt => opt.MapFrom(o => o.Groups.Count))
            .ForMember(sd => sd.OccupiedSeatCount, opt => opt.MapFrom(o => o.OrganizationUsers.Count(ou => ou.Status >= OrganizationUserStatusType.Invited)))
            .ForMember(sd => sd.OrganizationUsers, opt => opt.MapFrom(o => o.OrganizationUsers))
            .ForMember(sd => sd.ScimConnections, opt => opt.MapFrom(o => o.Connections.Where(c => c.Type == OrganizationConnectionType.Scim)))
            .ForMember(sd => sd.SsoConfig, opt => opt.MapFrom(o => o.SsoConfigs.SingleOrDefault()));
    }
}
