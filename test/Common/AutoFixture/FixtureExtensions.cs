using System;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Kernel;

namespace Bit.Test.Common.AutoFixture
{
    public static class FixtureExtensions
    {
        private static object GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
        public static IFixture WithAutoNSubstitutions(this IFixture fixture) =>
            fixture.Customize(new AutoNSubstituteCustomization());

        // This is the equivalent of fixture.Create<parameterInfo.ParameterType>, but no overload for
        // Create(Type type) exists.
        public static object Create(this IFixture fixture, Type type) =>
            new SpecimenContext(fixture).Resolve(new SeededRequest(type, GetDefault(type)));
    }
}
