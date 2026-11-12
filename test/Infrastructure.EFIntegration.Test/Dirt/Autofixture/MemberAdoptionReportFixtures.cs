using AutoFixture;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EntityFramework.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using EfAdminConsoleRepo = Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using EfVaultRepo = Bit.Infrastructure.EntityFramework.Vault.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class EfMemberAdoptionReport : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new DeviceBuilder());
        fixture.Customizations.Add(new CollectionBuilder());
        fixture.Customizations.Add(new GroupBuilder());
        fixture.Customizations.Add(new CipherBuilder());
        fixture.Customizations.Add(new OrganizationSponsorshipBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<MemberAdoptionReportRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<EfAdminConsoleRepo.OrganizationUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<DeviceRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<EfAdminConsoleRepo.CollectionRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<EfAdminConsoleRepo.GroupRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<EfVaultRepo.CipherRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CollectionCipherRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationSponsorshipRepository>());
    }
}

internal class EfMemberAdoptionReportAutoDataAttribute : CustomAutoDataAttribute
{
    public EfMemberAdoptionReportAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfMemberAdoptionReport())
    { }
}
