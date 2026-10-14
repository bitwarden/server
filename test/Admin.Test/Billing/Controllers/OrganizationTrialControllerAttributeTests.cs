using System.Reflection;
using Bit.Admin.Billing.Controllers;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Test.Billing.Controllers;

/// <summary>
/// Pins the declarative security boundary of the trial extension endpoint. The action mutates a customer's
/// Stripe subscription, so losing any one of these attributes in a refactor would silently widen access.
/// </summary>
public class OrganizationTrialControllerAttributeTests
{
    private static readonly Type _controller = typeof(OrganizationTrialController);
    private static readonly MethodInfo _extend = _controller.GetMethod(nameof(OrganizationTrialController.ExtendAsync))!;

    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        Assert.NotNull(_controller.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Controller_IsCloudOnly()
    {
        var selfHosted = _controller.GetCustomAttribute<SelfHostedAttribute>();

        Assert.NotNull(selfHosted);
        Assert.True(selfHosted.NotSelfHostedOnly);
    }

    [Fact]
    public void Extend_RequiresExtendTrialPermission()
    {
        var requirePermission = _extend.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(requirePermission);
        Assert.Equal(Permission.Org_ExtendTrial, requirePermission.Permission);
    }

    [Fact]
    public void Extend_IsPostOnlyWithAntiForgery()
    {
        Assert.NotNull(_extend.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(_extend.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Controller_HasNoOtherPublicActions()
    {
        var actions = _controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal([nameof(OrganizationTrialController.ExtendAsync)], actions);
    }
}
