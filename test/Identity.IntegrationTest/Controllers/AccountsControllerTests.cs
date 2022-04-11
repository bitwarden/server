using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.ApplicationFactories;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers
{
    public class AccountsControllerTests : IClassFixture<IdentityApplicationFactory>
    {
        private readonly IdentityApplicationFactory _factory;

        public AccountsControllerTests(IdentityApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostRegister_Success()
        {
            var context = await _factory.RegisterAsync(new RegisterRequestModel
            {
                Email = "test+register@email.com",
                MasterPasswordHash = "master_password_hash"
            });

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var database = _factory.GetDatabaseContext();
            var user = await database.Users
                .SingleAsync(u => u.Email == "test+register@email.com");

            Assert.NotNull(user);
        }
    }
}
