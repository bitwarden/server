using AutoFixture;
using Bit.Core.Settings;

namespace Bit.Test.Common.AutoFixture;

public class GlobalSettingsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<GlobalSettings>(composer => composer
            .Without(s => s.BaseServiceUri)
            .Without(s => s.Attachment)
            .Without(s => s.Send)
            .Without(s => s.DataProtection));
    }
}
