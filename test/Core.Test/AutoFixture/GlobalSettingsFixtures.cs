using System.Reflection;
using System.Text;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Core;
using Bit.Core.Test.Helpers.Factories;
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
                .Returns<byte[]>(data => Encoding.UTF8.GetBytes("P|" + Encoding.UTF8.GetString(data))); // I THINK?

            var dataProtectionProvider = new Mock<IDataProtectionProvider>();
            dataProtectionProvider
                .Setup(x => x.CreateProtector(Constants.DatabaseFieldProtectorPurpose))
                .Returns(dataProtector.Object);

            return dataProtectionProvider.Object;
        }

        return new NoSpecimen();
    }
}

public class GlobalSettingsCustomizeAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter) => new GlobalSettings();
}
