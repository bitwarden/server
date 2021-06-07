using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Bit.Core.Test.Helpers.Factories;

namespace Bit.Core.Test.AutoFixture.GlobalSettingsFixtures
{
    internal class GlobalSettingsBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var pi = request as ParameterInfo;
            if (pi == null || pi.ParameterType != typeof(GlobalSettings))
                return new NoSpecimen();

            return GlobalSettingsFactory.GlobalSettings;
        }
    }

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
