using AutoFixture;

namespace Bit.Test.Common.AutoFixture.Attributes;

/// <summary>
/// Disables setting of Auto Properties on the Controller to avoid ASP.net initialization errors from a mock environment. Still sets constructor dependencies.
/// </summary>
public class ControllerCustomizeAttribute : BitCustomizeAttribute
{
    private readonly Type _controllerType;

    /// <summary>
    /// Initialize an instance of the ControllerCustomizeAttribute class
    /// </summary>
    /// <param name="controllerType">The Type of the controller to allow autofixture to create</param>
    public ControllerCustomizeAttribute(Type controllerType)
    {
        _controllerType = controllerType;
    }

    public override ICustomization GetCustomization() => new ControllerCustomization(_controllerType);
}
