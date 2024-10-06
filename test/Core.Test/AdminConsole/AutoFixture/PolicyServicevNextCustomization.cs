#nullable enable

using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

/// <summary>
/// Configures Autofixture to inject the provided IPolicyDefinition implementations into the PolicyService constructor.
/// Note that this should usually be used even to inject an empty list, otherwise AutoFixture will create duplicate
/// invalid IPolicyDefinitions.
/// </summary>
public class PolicyServicevNextBuilder : ISpecimenBuilder
{
    private readonly IEnumerable<IPolicyDefinition> _policyDefinitions;

    public PolicyServicevNextBuilder(IEnumerable<IPolicyDefinition> policyDefinitions)
    {
        _policyDefinitions = policyDefinitions;
    }

    public object Create(object request, ISpecimenContext context)
    {
        var pi = request as ParameterInfo;
        if (pi == null)
            return new NoSpecimen();

        if (pi.Member.DeclaringType != typeof(PolicyServicevNext) ||
            pi.ParameterType != typeof(IEnumerable<IPolicyDefinition>))
        {
            return new NoSpecimen();
        }

        return _policyDefinitions;
    }
}
