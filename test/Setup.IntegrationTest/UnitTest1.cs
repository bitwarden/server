using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;

namespace Setup.IntegrationTest;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), Path.Combine("test", "Setup.IntegrationTest"))
            .WithContextDirectory(CommonDirectoryPath.GetSolutionDirectory().DirectoryPath)
            .WithDeleteIfExists(true)
            .WithLogger(loggerFactory.CreateLogger("SetupCreation"))
            .Build();

        await image.CreateAsync(TestContext.Current.CancellationToken);
    }
}
