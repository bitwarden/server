using AutoFixture;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;

public class OrganizationSponsorshipCustomizeAttribute : BitCustomizeAttribute
{
    public bool ToDelete = false;
    public override ICustomization GetCustomization() => ToDelete ?
        new ToDeleteOrganizationSponsorship() :
        new ValidOrganizationSponsorship();
}

public class ValidOrganizationSponsorship : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationSponsorship>(composer => composer
            .With(s => s.ToDelete, false)
            .With(s => s.LastSyncDate, DateTime.UtcNow.AddDays(new Random().Next(-90, 0))));
    }
}

public class ToDeleteOrganizationSponsorship : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationSponsorship>(composer => composer
            .With(s => s.ToDelete, true));
    }
}
