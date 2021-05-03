using AutoFixture;

namespace Bit.Core.Test.AutoFixture
{
    internal class GlobalSettings : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Settings.GlobalSettings>(composer => composer
                .Without(s => s.BaseServiceUri)
                .Without(s => s.Attachment)
                .Without(s => s.Send)
                .Without(s => s.DataProtection));
        }
    }
}
