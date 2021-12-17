using System;
using AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Api.Test.AutoFixture.Attributes
{
    public class ControllerCustomizeAttribute : BitCustomizeAttribute
    {
        private readonly Type _controllerType;
        public ControllerCustomizeAttribute(Type controllerType)
        {
            _controllerType = controllerType;
        }

        public override ICustomization GetCustomization() => new ControllerCustomization(_controllerType);
    }
}
