#nullable enable

using System.Reflection;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

/// <summary>
/// Configures Autofixture to inject the provided IPolicyValidator implementations into the PolicyService constructor.
/// Note that this should usually be used even to inject an empty list, otherwise AutoFixture will create duplicate
/// invalid IPolicyValidators.
/// </summary>
public class SavePolicyCommandSpecimenBuilder : ISpecimenBuilder
{
    private readonly IEnumerable<IPolicyValidator> _policyValidators;

    public SavePolicyCommandSpecimenBuilder(IEnumerable<IPolicyValidator> policyValidators)
    {
        _policyValidators = policyValidators;
    }

    public object Create(object request, ISpecimenContext context)
    {
        var pi = request as ParameterInfo;
        if (pi == null)
            return new NoSpecimen();

        if (pi.Member.DeclaringType != typeof(SavePolicyCommand) ||
            pi.ParameterType != typeof(IEnumerable<IPolicyValidator>))
        {
            return new NoSpecimen();
        }

        return _policyValidators;
    }
}
