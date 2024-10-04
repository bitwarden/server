using AutoFixture;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Billing.AutoFixture;

public class UserLicenseCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new UserLicenseCustomization();
}

public class UserLicenseCustomization : ICustomization
{
    public void Customize(IFixture fixture) =>
        fixture.Customize<UserLicense>(composer => composer
            .With(u => u.Issued, DateTime.UtcNow.AddDays(-10))
            .With(u => u.Expires, DateTime.UtcNow.AddDays(10))
            .With(u => u.Version, UserLicense.CurrentLicenseFileVersion + 1)
        );
}
