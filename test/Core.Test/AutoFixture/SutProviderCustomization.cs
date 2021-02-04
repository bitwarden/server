using System;
using AutoFixture;
using AutoFixture.Kernel;

namespace Bit.Core.Test.AutoFixture
{
    public class SutProviderCustomization : ICustomization, ISpecimenBuilder
    {
        private IFixture _fixture = null;

        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (!(request is Type typeRequest))
            {
                return new NoSpecimen();
            }
            if (!typeof(ISutProvider).IsAssignableFrom(typeRequest))
            {
                return new NoSpecimen();
            }

            return ((ISutProvider)Activator.CreateInstance(typeRequest, _fixture)).Create();
        }

        public void Customize(IFixture fixture)
        {
            _fixture = fixture;
            fixture.Customizations.Add(this);
        }
    }
}
