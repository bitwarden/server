using AutoFixture;
using Bit.Core.Billing.Organizations.Models;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Billing.AutoFixture;

public class OrganizationLicenseCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new OrganizationLicenseCustomization();
}
public class OrganizationLicenseCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationLicense>(composer => composer
            .With(o => o.Signature, Guid.NewGuid().ToString().Replace('-', '+')));
    }
}
