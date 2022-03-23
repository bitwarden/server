using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Bit.Api.IntegrationTest
{
    public class ApiApplicationFactory : WebApplicationFactory<Startup>
    {
    }
}
