using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.CurrentContextFixtures;

internal class CurrentContext : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new CurrentContextBuilder());
    }
}

internal class CurrentContextBuilder : ISpecimenBuilder
{
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
        if (typeof(ICurrentContext) != typeRequest)
        {
            return new NoSpecimen();
        }

        var obj = new Fixture().WithAutoNSubstitutions().Create<ICurrentContext>();
        obj.Organizations = context.Create<List<CurrentContextOrganization>>();
        return obj;
    }
}

internal class CurrentContextCustomize : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new CurrentContext();
}
