#nullable enable
using System.Reflection;
using System.Text;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core;
using Bit.Core.Settings;
using Bit.Core.Test.Helpers.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;

namespace Bit.Test.Common.AutoFixture;

public class GlobalSettingsBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is ParameterInfo pi)
        {
            if (pi.ParameterType == typeof(GlobalSettings))
            {
                return GlobalSettingsFactory.Create();
            }

            if (pi.ParameterType == typeof(IDataProtectionProvider))
            {
                var dataProtector = Substitute.For<IDataProtector>();
                dataProtector.Unprotect(Arg.Any<byte[]>())
                    .Returns(data =>
                        Encoding.UTF8.GetBytes(Constants.DatabaseFieldProtectedPrefix +
                                               Encoding.UTF8.GetString((byte[])data[0])));

                var dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
                dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose)
                    .Returns(dataProtector);

                return dataProtectionProvider;
            }
        }

        if (request is Type type && type == typeof(GlobalSettings))
        {
            return GlobalSettingsFactory.Create();
        }

        return new NoSpecimen();
    }
}

public class GlobalSettingsFromConfigurationCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new GlobalSettingsBuilder());
    }
}

public class GlobalSettingsCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new GlobalSettingsFromConfigurationCustomization();
}
