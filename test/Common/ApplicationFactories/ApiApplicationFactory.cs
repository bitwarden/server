using System.Net.Http;
using Bit.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Bit.Test.Common.ApplicationFactories
{
    public class ApiApplicationFactory : WebApplicationFactory<Startup>
    {
    }
}
