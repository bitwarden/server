using System;
using Xunit.Sdk;
using Xunit;
using AutoFixture;
using System.Reflection;
using System.Collections.Generic;
using AutoFixture.Xunit2;
using System.Linq;
using AutoFixture.Kernel;
using System.Collections;

namespace Bit.Test.Common.AutoFixture.Attributes
{
    public class MemberAutoDataAttribute : MemberDataAttributeBase
    {
        private readonly Func<IFixture> _createFixture;

        public MemberAutoDataAttribute(string memberName, params object[] parameters) :
            this(() => new Fixture(), memberName, parameters)
        { }

        public MemberAutoDataAttribute(Func<IFixture> createFixture, string memberName, params object[] parameters) :
            base(memberName, parameters)
        {
            _createFixture = createFixture;
        }

        protected override object[] ConvertDataItem(MethodInfo testMethod, object item)
        {
            var methodParameters = testMethod.GetParameters();
            var classCustomizations = testMethod.DeclaringType.GetCustomAttributes<BitCustomizeAttribute>().Select(attr => attr.GetCustomization());
            var methodCustomizations = testMethod.GetCustomAttributes<BitCustomizeAttribute>().Select(attr => attr.GetCustomization());

            var array = item as object[] ?? Array.Empty<object>();

            var fixture = ApplyCustomizations(ApplyCustomizations(_createFixture(), classCustomizations), methodCustomizations);
            var missingParameters = methodParameters.Skip(array.Length).Select(p => CustomizeAndCreate(p, fixture));

            return array.Concat(missingParameters).ToArray();
        }

        private static object CustomizeAndCreate(ParameterInfo p, IFixture fixture)
        {
            var customizations = p.GetCustomAttributes(typeof(CustomizeAttribute), false)
                .OfType<CustomizeAttribute>()
                .Select(attr => attr.GetCustomization(p));

            var context = new SpecimenContext(ApplyCustomizations(fixture, customizations));
            return context.Resolve(p);
        }

        private static IFixture ApplyCustomizations(IFixture fixture, IEnumerable<ICustomization> customizations)
        {
            var newFixture = new Fixture();

            foreach (var customization in fixture.Customizations.Reverse().Select(b => b.ToCustomization()))
            {
                newFixture.Customize(customization);
            }

            foreach (var customization in customizations)
            {
                newFixture.Customize(customization);
            }

            return newFixture;
        }
    }
}
