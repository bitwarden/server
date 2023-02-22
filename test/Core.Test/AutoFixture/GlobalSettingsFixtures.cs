﻿using System.Reflection;
using System.Text;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Core;
using Bit.Core.Test.Helpers.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Moq;

namespace Bit.Test.Common.AutoFixture;

public class GlobalSettingsBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var fixture = new Fixture();

        if (request is not ParameterInfo pi)
        {
            return new NoSpecimen();
        }

        if (pi.ParameterType == typeof(Bit.Core.Settings.GlobalSettings))
        {
            return GlobalSettingsFactory.GlobalSettings;
        }

        if (pi.ParameterType == typeof(IDataProtectionProvider))
        {
            var dataProtector = new Mock<IDataProtector>();
            dataProtector
                .Setup(d => d.Unprotect(It.IsAny<byte[]>()))
                .Returns<byte[]>(data => Encoding.UTF8.GetBytes(Constants.DatabaseFieldProtectedPrefix + Encoding.UTF8.GetString(data)));

            var dataProtectionProvider = new Mock<IDataProtectionProvider>();
            dataProtectionProvider
                .Setup(x => x.CreateProtector(Constants.DatabaseFieldProtectorPurpose))
                .Returns(dataProtector.Object);

            return dataProtectionProvider.Object;
        }

        return new NoSpecimen();
    }
}

internal class SelfHosted : ICustomization
{
    public bool IsSelfHosted { get; set; }
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new GlobalSettingsBuilder());

        fixture.Customize<Bit.Core.Settings.GlobalSettings>(composer => composer
            .With(o => o.SelfHosted, IsSelfHosted));
    }
}

public class GlobalSettingsCustomizeAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter) => new GlobalSettings();
}

internal class SelfHostedAutoDataAttribute : CustomAutoDataAttribute
{
    public SelfHostedAutoDataAttribute(bool isSelfHosted) : base(new SutProviderCustomization(), new SelfHosted() { IsSelfHosted = isSelfHosted })
    { }
}
