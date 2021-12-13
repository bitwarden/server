using System;
using System.Reflection;
using AutoFixture.Kernel;
using Bit.Core.Settings;

namespace Bit.Core.Test.AutoFixture
{
    public class IgnoreVirtualMembersCustomization : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var pi = request as PropertyInfo;
            if (pi == null)
            {
                return new NoSpecimen();
            }

            if (pi.GetGetMethod().IsVirtual && pi.DeclaringType != typeof(GlobalSettings))
            {
                return null;
            }
            return new NoSpecimen();
        }
    }
}
