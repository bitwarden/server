using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser;

/// <summary>
/// Customization that enables the PolicyRequirements feature flag for this sut.
/// </summary>
public class RestoreOrganizationUserCommandCustomization : SutProviderCustomization
{
    public override object Create(object request, ISpecimenContext context)
    {
        var specimen = base.Create(request, context);
        if (specimen is NoSpecimen)
        {
            return specimen;
        }

        var sutProvider = (SutProvider<RestoreOrganizationUserCommand>)specimen;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);

        return specimen;
    }
}

public class RestoreOrganizationUserCommandCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new RestoreOrganizationUserCommandCustomization();
}
