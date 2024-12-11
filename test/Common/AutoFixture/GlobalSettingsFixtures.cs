using AutoFixture;

namespace Bit.Test.Common.AutoFixture;

public class GlobalSettings : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Bit.Core.Settings.GlobalSettings>(composer =>
            composer
                .Without(s => s.BaseServiceUri)
                .Without(s => s.Attachment)
                .Without(s => s.Send)
                .Without(s => s.DataProtection)
        );
    }
}
