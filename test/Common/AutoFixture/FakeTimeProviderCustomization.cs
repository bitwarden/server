#nullable enable

using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Bit.Test.Common.AutoFixture;

/// <summary>
/// Configures Autofixture to use the FakeTimeProvider implementation in tests
/// </summary>
public class FakeTimeProviderBuilder : ISpecimenBuilder
{
    private static readonly FakeTimeProvider _fakeTimeProvider = new();

    public object Create(object request, ISpecimenContext context)
    {
        var pi = request as ParameterInfo;
        if (pi == null)
        {
            return new NoSpecimen();
        }

        if (pi.ParameterType != typeof(TimeProvider))
        {
            return new NoSpecimen();
        }

        return _fakeTimeProvider;
    }
}

public class FakeTimeProviderCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new FakeTimeProviderBuilder());
    }
}
