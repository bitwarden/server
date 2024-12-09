using System.Reflection;
using System.Text;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Core;
using Bit.Core.Test.Helpers.Factories;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;

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

        return new NoSpecimen();
    }
}

public class GlobalSettingsCustomizeAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter) => new GlobalSettings();
}
