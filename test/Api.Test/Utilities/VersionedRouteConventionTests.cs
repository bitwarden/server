using System.Reflection;
using Bit.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class VersionedRouteConventionTests
{
    private readonly VersionedRouteConvention _sut = new();

    [Fact]
    public void Apply_ActionWithoutAttribute_DoesNotModifySelectors()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.Unversioned),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: "{id}");

        var originalTemplate = action.Selectors[0].AttributeRouteModel!.Template;

        // Act
        _sut.Apply(action);

        // Assert
        Assert.Equal(originalTemplate, action.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_ActionWithVersionedRoute_RewritesToAbsoluteVersionedPath()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV2),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: "{id}/reset-password");

        // Act
        _sut.Apply(action);

        // Assert
        Assert.Equal("/v2/organizations/{orgId}/users/{id}/reset-password",
            action.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_ActionWithNoActionRoute_UsesControllerRouteOnly()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV2),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: null);

        // Act
        _sut.Apply(action);

        // Assert
        Assert.Equal("/v2/organizations/{orgId}/users",
            action.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_ControllerWithNoRoute_ThrowsInvalidOperationException()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV2),
            controllerRoute: null,
            actionRoute: "{id}");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _sut.Apply(action));
        Assert.Contains("must have a [Route] attribute", ex.Message);
        Assert.Contains(nameof(FakeController), ex.Message);
    }

    [Fact]
    public void Apply_MultipleSelectors_RewritesAllSelectors()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV2),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: "{id}");

        // Add a second selector
        action.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "{id}/details" }
        });

        // Act
        _sut.Apply(action);

        // Assert
        Assert.Equal("/v2/organizations/{orgId}/users/{id}",
            action.Selectors[0].AttributeRouteModel!.Template);
        Assert.Equal("/v2/organizations/{orgId}/users/{id}/details",
            action.Selectors[1].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_NullAttributeRouteModelOnSelector_CreatesOne()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV2),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: null);

        // Explicitly null out the AttributeRouteModel
        action.Selectors[0].AttributeRouteModel = null;

        // Act
        _sut.Apply(action);

        // Assert
        Assert.NotNull(action.Selectors[0].AttributeRouteModel);
        Assert.Equal("/v2/organizations/{orgId}/users",
            action.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_VersionOne_ProducesV1Prefix()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV1),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: "{id}");

        // Act
        _sut.Apply(action);

        // Assert
        Assert.Equal("/v1/organizations/{orgId}/users/{id}",
            action.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_EmptyActionRoute_UsesControllerRouteOnly()
    {
        // Arrange
        var action = CreateActionModel<FakeController>(
            nameof(FakeController.VersionedV2),
            controllerRoute: "organizations/{orgId}/users",
            actionRoute: "");

        // Act
        _sut.Apply(action);

        // Assert
        Assert.Equal("/v2/organizations/{orgId}/users",
            action.Selectors[0].AttributeRouteModel!.Template);
    }

    /// <summary>
    /// Builds a minimal <see cref="ActionModel"/> wired to a <see cref="ControllerModel"/>
    /// with the specified route templates.
    /// </summary>
    private static ActionModel CreateActionModel<TController>(
        string methodName,
        string? controllerRoute,
        string? actionRoute)
    {
        var typeInfo = typeof(TController).GetTypeInfo();
        var methodInfo = typeInfo.GetMethod(methodName)!;

        var controllerModel = new ControllerModel(typeInfo, typeInfo.GetCustomAttributes().ToArray());
        if (controllerRoute is not null)
        {
            controllerModel.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel { Template = controllerRoute }
            });
        }
        else
        {
            // Controller with no route — add a selector without a route model
            controllerModel.Selectors.Add(new SelectorModel());
        }

        var actionModel = new ActionModel(methodInfo, methodInfo.GetCustomAttributes().ToArray())
        {
            Controller = controllerModel
        };

        actionModel.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = actionRoute is not null
                ? new AttributeRouteModel { Template = actionRoute }
                : null
        });

        controllerModel.Actions.Add(actionModel);

        return actionModel;
    }

    /// <summary>
    /// Fake controller used only to provide real <see cref="MethodInfo"/> instances
    /// with the correct attribute metadata for the convention to inspect.
    /// </summary>
    [Route("fake")]
    private class FakeController
    {
        public void Unversioned() { }

        [VersionedRoute(1)]
        public void VersionedV1() { }

        [VersionedRoute(2)]
        public void VersionedV2() { }
    }
}
