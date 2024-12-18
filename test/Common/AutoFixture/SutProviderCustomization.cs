using AutoFixture;
using AutoFixture.Kernel;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class SutProviderCustomization(bool create = true) : ICustomization, ISpecimenBuilder
{
    private IFixture _fixture = null;

    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!(request is Type typeRequest))
        {
            return new NoSpecimen();
        }

        if (!typeof(ISutProvider).IsAssignableFrom(typeRequest))
        {
            return new NoSpecimen();
        }

        var sutProvider = (ISutProvider)Activator.CreateInstance(typeRequest, _fixture);

        return create ? sutProvider?.Create() : sutProvider;
    }

    public void Customize(IFixture fixture)
    {
        _fixture = fixture;
        fixture.Customizations.Add(this);
    }
}
