using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;


public static class SwaggerGenOptionsExt
{

    public static void InitializeSwaggerFilters(
    this SwaggerGenOptions config, IWebHostEnvironment environment)
    {
        config.SchemaFilter<EnumSchemaFilter>();
        config.SchemaFilter<EncryptedStringSchemaFilter>();

        config.OperationFilter<ActionNameOperationFilter>();

        // Set the operation ID to the name of the controller followed by the name of the function.
        // Note that the "Controller" suffix for the controllers, and the "Async" suffix for the actions
        // are removed already, so we don't need to do that ourselves.
        config.CustomOperationIds(e => $"{e.ActionDescriptor.RouteValues["controller"]}_{e.ActionDescriptor.RouteValues["action"]}");
        config.DocumentFilter<SwaggerExcludeDocumentFilter>();
        config.DocumentFilter<CheckDuplicateOperationIdsDocumentFilter>();

        // These two filters require debug symbols/git, so only add them in development mode
        if (environment.IsDevelopment())
        {
            config.DocumentFilter<GitCommitDocumentFilter>();
            config.OperationFilter<SourceFileLineOperationFilter>();
        }

    }
}
