using System.Diagnostics.Metrics;
using AutoFixture;
using NSubstitute;

namespace Bit.Test.Common.AutoFixture.Attributes;

/// <summary>
/// Customizes a <see cref="IMeterFactory"/> to be able to actually create <see cref="Meter"/>'s.
/// </summary>
public class MeterCustomizeAttribute : BitCustomizeAttribute
{
    private static readonly MeterCustomization _meterCustomization = new();
    public override ICustomization GetCustomization() => _meterCustomization;

    private class MeterCustomization : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<IMeterFactory>(factory =>
            {
                return factory.FromFactory(() =>
                {
                    var fakeFactory = Substitute.For<IMeterFactory>();
                    fakeFactory.Create(Arg.Any<MeterOptions>())
                        .Returns((call) =>
                        {
                            return new Meter(call.Arg<MeterOptions>());
                        });

                    return fakeFactory;
                });
            });
        }
    }
}
