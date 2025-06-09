using System.Diagnostics;
using Bit.Core.Auth.UserFeatures.Registration.VerifyEmail;
using Bit.Core.Platform.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;


namespace Bit.Core.Test.Platform;

public class MailerTest
{
    [Fact]
    public async Task SendEmailAsync()
    {

        var services = new ServiceCollection();

        var appDirectory = Directory.GetCurrentDirectory();

        var webhostBuilder = new WebHostBuilder().ConfigureServices(services =>
        {
            services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
            {
                options.FileProviders.Clear();
                options.FileProviders.Add(new EmbeddedFileProvider(typeof(Mailer).Assembly, "Bit.Core"));
            });

            var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<DiagnosticListener>(diagnosticSource);
            services.AddSingleton<DiagnosticSource>(diagnosticSource);

            services.AddLogging();
            services.AddMvc().AddRazorRuntimeCompilation();
            services.AddRazorPages();

            services.AddSingleton<IRazorViewToStringRenderer, RazorViewToStringRenderer>();

        }).UseStartup<Startup>().Build();


        var razorViewToStringRenderer = webhostBuilder.Services.GetRequiredService<IRazorViewToStringRenderer>();
        var mailer = new Mailer(new RazorMailRenderer(razorViewToStringRenderer));


        //var mailer = new Mailer(new HandlebarMailRenderer());

        var mail = new VerifyEmail
        {
            Token = "test-token",
            Email = "test@bitwarden.com",
            WebVaultUrl = "https://vault.bitwarden.com"
        };

        await mailer.SendEmail(mail, "test@bitwarden.com");
    }
}


internal class Startup
{
    public void Configure()
    {

    }
}
