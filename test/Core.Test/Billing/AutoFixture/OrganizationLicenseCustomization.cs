using AutoFixture;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Billing.AutoFixture;

public class OrganizationLicenseCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new OrganizationLicenseCustomization();
}

public class OrganizationLicenseCustomization : ICustomization
{
    public void Customize(IFixture fixture) =>
        fixture.Customize<OrganizationLicense>(composer => composer
            .With(o => o.Signature, Guid.NewGuid().ToString().Replace('-', '+'))
            .With(o => o.Issued, DateTime.UtcNow.AddDays(-10))
            .With(o => o.Expires, DateTime.UtcNow.AddDays(10))
            .With(o => o.Version, OrganizationLicense.CurrentLicenseFileVersion + 1)
            .With(o => o.InstallationId, Guid.Empty)
            .With(o => o.Enabled, true)
            .With(o => o.PlanType, PlanType.EnterpriseAnnually)
        );
}
