using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Bit.Core.Test.AutoFixture
{
    public static class FixtureExtensions
    {
        public static IFixture WithAutoNSubstitutions(this IFixture fixture)
            => fixture.Customize(new AutoNSubstituteCustomization());
    }
}
