using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.Test.Utilities;

public static class ControllerAuthorizationTestHelpers
{
    private static readonly Type[] _httpMethodAttributes =
    [
        typeof(HttpGetAttribute),
        typeof(HttpPostAttribute),
        typeof(HttpPutAttribute),
        typeof(HttpDeleteAttribute),
        typeof(HttpPatchAttribute),
        typeof(HttpHeadAttribute),
        typeof(HttpOptionsAttribute)
    ];

    /// <summary>
    /// Asserts that a controller follows the two-layer authorization pattern required by Bitwarden.
    /// </summary>
    /// <param name="controllerType">The controller type to validate.</param>
    /// <remarks>
    /// This enforces two requirements:
    /// <list type="number">
    /// <item>
    /// <description>
    /// Class-level requirement: The controller MUST have a class-level <c>[Authorize]</c> attribute.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Method-level requirement: Every HTTP action method MUST have either:
    /// <list type="bullet">
    /// <item><description>Any custom <c>AuthorizeAttribute</c> implementation (e.g., <c>[Authorize&lt;TRequirement&gt;]</c>) for protected endpoints, OR</description></item>
    /// <item><description><c>[AllowAnonymous]</c> for intentionally public endpoints</description></item>
    /// </list>
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// This ensures that every route is explicitly decorated with authorization, preventing accidental
    /// exposure of endpoints. The class-level <c>[Authorize]</c> alone is necessary but not sufficient.
    /// Note that the base <c>[Authorize]</c> attribute is not accepted at the method level.
    /// </para>
    /// </remarks>
    /// <exception cref="Xunit.Sdk.FailException">
    /// Thrown when the controller is missing class-level authorization, has no HTTP methods,
    /// or has HTTP methods without explicit method-level authorization.
    /// </exception>
    /// <example>
    /// <code>
    /// [Fact]
    /// public void AllActionMethodsHaveAuthorization()
    /// {
    ///     ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
    ///         typeof(MyController));
    /// }
    /// </code>
    /// </example>
    public static void AssertAllHttpMethodsHaveAuthorization(Type controllerType)
    {
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var httpActionMethods = methods
            .Where(HasHttpMethodAttribute)
            .ToList();

        if (httpActionMethods.Count == 0)
        {
            Assert.Fail($"Controller {controllerType.Name} has no HTTP action methods.");
        }

        // REQUIRE class-level [Authorize]
        var classHasAuthorization = HasAuthorizeAttribute(controllerType);
        if (!classHasAuthorization)
        {
            Assert.Fail(
                $"Controller {controllerType.Name} is missing required class-level [Authorize] attribute.\n" +
                $"All controllers must have [Authorize] at the class level as a baseline security measure.");
        }

        // REQUIRE each method to have explicit authorization
        var unauthorizedMethods = new List<string>();

        foreach (var method in httpActionMethods)
        {
            // Only check for custom [Authorize<T>] (not base [Authorize])
            var methodHasCustomAuthorize = HasCustomAuthorizeAttribute(method);
            var methodHasAllowAnonymous = HasAllowAnonymousAttribute(method);

            // Method must have EITHER [Authorize<T>] OR [AllowAnonymous]
            var hasAuthorizationAttribute = methodHasCustomAuthorize || methodHasAllowAnonymous;

            if (!hasAuthorizationAttribute)
            {
                var httpAttributes = string.Join(", ",
                    method.GetCustomAttributes()
                        .Where(a => _httpMethodAttributes.Contains(a.GetType()))
                        .Select(a => $"[{a.GetType().Name.Replace("Attribute", "")}]"));

                unauthorizedMethods.Add($"{method.Name} ({httpAttributes})");
            }
        }

        if (unauthorizedMethods.Count != 0)
        {
            var methodList = string.Join("\n  - ", unauthorizedMethods);
            Assert.Fail(
                $"Controller {controllerType.Name} has {unauthorizedMethods.Count} HTTP action method(s) without method-level authorization:\n" +
                $"  - {methodList}\n\n" +
                $"Each HTTP action method must be explicitly decorated with:\n" +
                $"  - [Authorize<TRequirement>] for protected endpoints, OR\n" +
                $"  - [AllowAnonymous] for intentionally public endpoints\n\n" +
                $"Note: Class-level [Authorize] is required but not sufficient. Every route must be explicitly decorated.");
        }
    }

    private static bool HasHttpMethodAttribute(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Any(attr => _httpMethodAttributes.Contains(attr.GetType()));
    }

    /// <summary>
    /// Checks if a type or method has any [Authorize] attribute (including subclasses).
    /// Used for class-level checks.
    /// </summary>
    private static bool HasAuthorizeAttribute(MemberInfo member)
    {
        return member.GetCustomAttributes()
            .Any(attr => attr.GetType().IsAssignableTo(typeof(AuthorizeAttribute)));
    }

    /// <summary>
    /// Checks if a method has a custom (subclassed) [Authorize] attribute.
    /// Does NOT match the base [Authorize] attribute.
    /// Used for method-level checks.
    /// </summary>
    /// <remarks>
    /// We don't match the base [Authorize] attribute because we don't currently use this
    /// for role-based checks on methods, so it is unlikely to indicate a
    /// proper authorization check. This is based on current practice only and could be
    /// changed in the future if our practice changes.
    /// </remarks>
    private static bool HasCustomAuthorizeAttribute(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Select(attr => attr.GetType())
            .Any(attrType => attrType.IsSubclassOf(typeof(AuthorizeAttribute)));
    }

    private static bool HasAllowAnonymousAttribute(MethodInfo method)
    {
        return method.GetCustomAttribute<AllowAnonymousAttribute>() != null;
    }
}
