using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Models.Data;

namespace Bit.Core.Test.AutoFixture;

public class CollectionAccessSelectionCustomization : ICustomization
{
    public bool Manage { get; set; }

    public CollectionAccessSelectionCustomization(bool manage)
    {
        Manage = manage;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<CollectionAccessSelection>(composer => composer
            .With(o => o.Manage, Manage));
    }
}

public class CollectionAccessSelectionAttribute : CustomizeAttribute
{
    private readonly bool _manage;

    public CollectionAccessSelectionAttribute(bool manage = false)
    {
        _manage = manage;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new CollectionAccessSelectionCustomization(_manage);
    }
}
