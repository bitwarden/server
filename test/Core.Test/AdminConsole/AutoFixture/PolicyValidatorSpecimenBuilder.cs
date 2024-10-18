#nullable enable

using System.Reflection;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

/// <summary>
/// Configures Autofixture to inject the provided IPolicyValidator implementations.
/// This is used to inject mock implementations and to avoid invalid implementations
/// (e.g. duplicate implementations for the same PolicyType).
/// </summary>
public class PolicyValidatorSpecimenBuilder : ISpecimenBuilder
{
    private readonly IEnumerable<IPolicyValidator> _policyValidators;

    public PolicyValidatorSpecimenBuilder(IEnumerable<IPolicyValidator> policyValidators)
    {
        _policyValidators = policyValidators;
    }

    public object Create(object request, ISpecimenContext context)
    {
        var pi = request as ParameterInfo;
        if (pi == null)
            return new NoSpecimen();

        if (pi.ParameterType != typeof(IEnumerable<IPolicyValidator>))
        {
            return new NoSpecimen();
        }

        return _policyValidators;
    }
}
