using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Bit.Test.Common.AutoFixture;

public static class FixtureExtensions
{
    public static IFixture WithAutoNSubstitutions(this IFixture fixture) =>
        fixture.Customize(new AutoNSubstituteCustomization());

    public static IFixture WithAutoNSubstitutionsAutoPopulatedProperties(this IFixture fixture) =>
        fixture.Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });
}
