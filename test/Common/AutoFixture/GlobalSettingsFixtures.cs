using System;
using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Test.Common.Helpers.Factories;

namespace Bit.Test.Common.AutoFixture
{
    public class GlobalSettingsBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var pi = request as ParameterInfo;
            var fixture = new Fixture();

            if (pi == null || pi.ParameterType != typeof(Bit.Core.Settings.GlobalSettings))
                return new NoSpecimen();

            return GlobalSettingsFactory.GlobalSettings;
        }
    }

    public class GlobalSettings : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Bit.Core.Settings.GlobalSettings>(composer => composer
                .Without(s => s.BaseServiceUri)
                .Without(s => s.Attachment)
                .Without(s => s.Send)
                .Without(s => s.DataProtection));
        }
    }

    public class GlobalSettingsCustomizeAttribute : CustomizeAttribute
    {
        public override ICustomization GetCustomization(ParameterInfo parameter) => new GlobalSettings();
    }
}
