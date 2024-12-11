using AutoFixture;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture;

public class OrganizationLicenseCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new OrganizationLicenseCustomization();
}

public class OrganizationLicenseCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationLicense>(composer =>
            composer.With(o => o.Signature, Guid.NewGuid().ToString().Replace('-', '+'))
        );
    }
}
