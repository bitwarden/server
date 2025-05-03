using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Bit.Setup;

internal class SetupHostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = "Setup";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Production";
}
