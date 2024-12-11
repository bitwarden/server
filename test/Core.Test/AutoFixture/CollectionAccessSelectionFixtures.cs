using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Models.Data;

namespace Bit.Core.Test.AutoFixture;

public class CollectionAccessSelectionCustomization : ICustomization
{
    public bool Manage { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }

    public CollectionAccessSelectionCustomization(bool manage)
    {
        Manage = manage;
        ReadOnly = !manage;
        HidePasswords = !manage;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<CollectionAccessSelection>(composer =>
            composer
                .With(o => o.Manage, Manage)
                .With(o => o.ReadOnly, ReadOnly)
                .With(o => o.HidePasswords, HidePasswords)
        );
    }
}

public class CollectionAccessSelectionCustomizeAttribute : CustomizeAttribute
{
    private readonly bool _manage;

    public CollectionAccessSelectionCustomizeAttribute(bool manage = false)
    {
        _manage = manage;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new CollectionAccessSelectionCustomization(_manage);
    }
}
