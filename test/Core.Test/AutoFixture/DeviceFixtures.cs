using AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture
{
    public class Device : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Create<Core.Models.Table.Device>();
        }
    }

    internal class DeviceAutoDataAttribute : CustomAutoDataAttribute
    {
        public DeviceAutoDataAttribute() : base(new SutProviderCustomization(), new Device())
        { }
    }
}
