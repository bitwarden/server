using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class OrganizationUserBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == typeof(OrganizationUserCustomization))
        {
            var fixture = new Fixture();
            var orgUser = fixture.WithAutoNSubstitutions().Create<OrganizationUser>();
            var orgUserPermissions = fixture.WithAutoNSubstitutions().Create<Permissions>();
            orgUser.Permissions = JsonSerializer.Serialize(orgUserPermissions, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            return orgUser;
        }
        else if (type == typeof(List<OrganizationUserCustomization>))
        {
            var fixture = new Fixture();
            var orgUsers = fixture.WithAutoNSubstitutions().CreateMany<OrganizationUser>(2);
            foreach (var orgUser in orgUsers)
            {
                var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
                var orgUserPermissions = fixture.WithAutoNSubstitutions().Create<Permissions>();
                orgUser.Permissions = JsonSerializer.Serialize(orgUserPermissions, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
            }
            return orgUsers;
        }
        return new NoSpecimen();
    }
}

internal class EfOrganizationUser : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new OrganizationUserBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfOrganizationUserAutoDataAttribute : CustomAutoDataAttribute
{
    public EfOrganizationUserAutoDataAttribute() : base(new SutProviderCustomization(), new EfOrganizationUser())
    { }
}

internal class InlineEfOrganizationUserAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfOrganizationUserAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
        typeof(EfOrganizationUser) }, values)
    { }
}
