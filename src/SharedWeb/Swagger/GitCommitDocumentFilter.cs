#nullable enable

using System.Diagnostics;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Add the Git commit that was used to generate the Swagger document, to help with debugging and reproducibility.
/// </summary>
public class GitCommitDocumentFilter : IDocumentFilter
{

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (!string.IsNullOrEmpty(GitCommit))
        {
            swaggerDoc.Extensions.Add("x-git-commit", new Microsoft.OpenApi.Any.OpenApiString(GitCommit));
        }
    }

    public static string? GitCommit => _gitCommit.Value;

    private static readonly Lazy<string?> _gitCommit = new(() =>
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var result = process.StandardOutput.ReadLine()?.Trim();
            process.WaitForExit();
            return result ?? string.Empty;
        }
        catch
        {
            return null;
        }
    });
}
