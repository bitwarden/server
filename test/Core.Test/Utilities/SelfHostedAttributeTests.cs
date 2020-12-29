using System.Collections.Generic;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class SelfHostedAttributeTests
    {
        [Fact]
        public void NotSelfHosted_Throws_When_SelfHosted()
        {
            var sha = new SelfHostedAttribute { NotSelfHostedOnly = true };

            Assert.Throws<BadRequestException>(() => sha.OnActionExecuting(GetContext(selfHosted: true)));
        }

        [Fact]
        public void NotSelfHosted_Success_When_NotSelfHosted()
        {
            var sha = new SelfHostedAttribute { NotSelfHostedOnly = true };

            sha.OnActionExecuting(GetContext(selfHosted: false));
        }


        [Fact]
        public void SelfHosted_Success_When_SelfHosted()
        {
            var sha = new SelfHostedAttribute { SelfHostedOnly = true };

            sha.OnActionExecuting(GetContext(selfHosted: true));
        }

        [Fact]
        public void SelfHosted_Throws_When_NotSelfHosted()
        {
            var sha = new SelfHostedAttribute { SelfHostedOnly = true };

            Assert.Throws<BadRequestException>(() => sha.OnActionExecuting(GetContext(selfHosted: false)));
        }


        // This generates a ActionExecutingContext with the needed injected
        // service with the given value.
        private ActionExecutingContext GetContext(bool selfHosted)
        {
            IServiceCollection services = new ServiceCollection();

            var globalSettings = new GlobalSettings
            {
                SelfHosted = selfHosted
            };

            services.AddSingleton(globalSettings);

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = services.BuildServiceProvider();

            var context = Substitute.For<ActionExecutingContext>(
                Substitute.For<ActionContext>(httpContext,
                    new RouteData(),
                    Substitute.For<ActionDescriptor>()), 
                new List<IFilterMetadata>(), 
                new Dictionary<string, object>(), 
                Substitute.For<Controller>());

            return context;
        }
    }
}