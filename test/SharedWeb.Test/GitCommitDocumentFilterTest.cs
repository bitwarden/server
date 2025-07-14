using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class GitCommitDocumentFilterTest
{
    [Fact]
    public void AddsGitCommitExtensionIfAvailable()
    {
        var doc = new OpenApiDocument();
        var context = new DocumentFilterContext(null, null, null);
        var filter = new GitCommitDocumentFilter();
        filter.Apply(doc, context);

        Assert.True(doc.Extensions.ContainsKey("x-git-commit"));
        var ext = doc.Extensions["x-git-commit"] as Microsoft.OpenApi.Any.OpenApiString;
        Assert.NotNull(ext);
        Assert.False(string.IsNullOrEmpty(ext.Value));

    }
}
