using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Models;

public class Secret : Core.SecretsManager.Entities.Secret
{
    public new virtual ICollection<Project> Projects { get; set; }
    public virtual Organization Organization { get; set; }
    public virtual ICollection<UserSecretAccessPolicy> UserAccessPolicies { get; set; }
    public virtual ICollection<GroupSecretAccessPolicy> GroupAccessPolicies { get; set; }
    public virtual ICollection<ServiceAccountSecretAccessPolicy> ServiceAccountAccessPolicies { get; set; }
}

public class SecretMapperProfile : Profile
{
    public SecretMapperProfile()
    {
        CreateMap<Core.SecretsManager.Entities.Secret, Secret>().PreserveReferences().ReverseMap();
    }
}
