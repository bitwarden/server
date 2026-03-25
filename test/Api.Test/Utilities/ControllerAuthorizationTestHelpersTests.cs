using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class ControllerAuthorizationTestHelpersTests
{
    [Fact]
    public void AssertAllHttpMethodsHaveAuthorization_ControllerMissingClassLevelAuthorize_Throws()
    {
        var exception = Assert.Throws<Xunit.Sdk.FailException>(() =>
            ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
                typeof(ControllerWithoutClassAuthorize)));

        Assert.Contains("missing required class-level [Authorize] attribute", exception.Message);
        Assert.Contains("ControllerWithoutClassAuthorize", exception.Message);
    }

    [Fact]
    public void AssertAllHttpMethodsHaveAuthorization_MethodMissingAuthorization_Throws()
    {
        var exception = Assert.Throws<Xunit.Sdk.FailException>(() =>
            ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
                typeof(ControllerWithUnauthorizedMethod)));

        Assert.Contains("3 HTTP action method(s) without method-level authorization", exception.Message);
        Assert.Contains("GetUnauthorized ([HttpGet])", exception.Message);
        Assert.Contains("PostUnauthorized ([HttpPost])", exception.Message);
        Assert.Contains("PutUnauthorized ([HttpPut])", exception.Message);
        Assert.Contains("ControllerWithUnauthorizedMethod", exception.Message);
    }

    [Fact]
    public void AssertAllHttpMethodsHaveAuthorization_AllMethodsProperlyAuthorized_DoesNotThrow()
    {
        ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
            typeof(ControllerWithProperAuthorization));
    }

    [Fact]
    public void AssertAllHttpMethodsHaveAuthorization_MethodWithAllowAnonymous_DoesNotThrow()
    {
        ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
            typeof(ControllerWithAllowAnonymous));
    }

    [Fact]
    public void AssertAllHttpMethodsHaveAuthorization_ControllerWithNoHttpMethods_Throws()
    {
        var exception = Assert.Throws<Xunit.Sdk.FailException>(() =>
            ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
                typeof(ControllerWithNoHttpMethods)));

        Assert.Contains("has no HTTP action methods", exception.Message);
    }

    // Controller missing class-level [Authorize]
    private class ControllerWithoutClassAuthorize : ControllerBase
    {
        [HttpGet]
        [CustomAuthorize]
        public IActionResult Get() => Ok();
    }

    // Controller with class-level [Authorize] but methods missing method-level authorization
    [Authorize]
    private class ControllerWithUnauthorizedMethod : ControllerBase
    {
        [HttpGet("authorized")]
        [CustomAuthorize]
        public IActionResult GetAuthorized() => Ok();

        [HttpGet("unauthorized")]
        public IActionResult GetUnauthorized() => Ok();

        [HttpPost("unauthorized")]
        public IActionResult PostUnauthorized() => Ok();

        [HttpPut("unauthorized")]
        public IActionResult PutUnauthorized() => Ok();

        // Non-HTTP method should be ignored
        public IActionResult NonHttpMethod() => Ok();
    }

    // Controller with proper authorization on all methods
    [Authorize]
    private class ControllerWithProperAuthorization : ControllerBase
    {
        [HttpGet("custom")]
        [CustomAuthorize]
        public IActionResult GetWithCustom() => Ok();

        [HttpPost("custom")]
        [CustomAuthorize]
        public IActionResult PostWithCustom() => Ok();

        [HttpDelete("custom")]
        [CustomAuthorize]
        public IActionResult DeleteWithCustom() => Ok();
    }

    // Controller with AllowAnonymous (which is valid method-level authorization)
    [Authorize]
    private class ControllerWithAllowAnonymous : ControllerBase
    {
        [HttpGet("anonymous")]
        [AllowAnonymous]
        public IActionResult GetAnonymous() => Ok();

        [HttpPost("protected")]
        [CustomAuthorize]
        public IActionResult PostProtected() => Ok();

        [HttpGet("mixed")]
        [AllowAnonymous]
        public IActionResult GetMixed() => Ok();
    }

    // Controller with no HTTP methods
    [Authorize]
    private class ControllerWithNoHttpMethods : ControllerBase
    {
        public IActionResult NotAnHttpMethod() => Ok();
        public void AnotherNonHttpMethod() { }
    }

    // Custom authorize attribute for testing (mimics [Authorize<T>])
    private class CustomAuthorizeAttribute : AuthorizeAttribute
    {
    }
}
