using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

/// <summary>
/// Override autofixture and set the injected PolicyDefinitions to an empty array.
/// This prevents Autofixture from creating duplicate PolicyDefinitions which will throw an error.
/// </summary>
public class PolicyServicevNextBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        var pi = request as ParameterInfo;
        if (pi == null)
            return new NoSpecimen();

        if (pi.Member.DeclaringType != typeof(PolicyServicevNext) ||
            pi.ParameterType != typeof(IEnumerable<IPolicyDefinition>))
            return new NoSpecimen();

        return Array.Empty<IPolicyDefinition>();
    }
}

public class PolicyServicevNextCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new PolicyServicevNextBuilder());
    }
}

/// <summary>
/// A customization for PolicyService that sets the injected PolicyDefinitions to an empty array.
/// This prevents Autofixture from creating duplicate PolicyDefinitions which will throw an error.
/// </summary>
public class PolicyServicevNextCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new PolicyServicevNextCustomization();
}
