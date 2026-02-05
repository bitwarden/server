using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using NSubstitute;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class SwaggerDocUtil
{
    /// <summary>
    /// Creates an OpenApiDocument and DocumentFilterContext from the specified controller type by setting up
    /// a minimal service collection and using the SwaggerProvider to generate the document.
    /// </summary>
    public static (OpenApiDocument, DocumentFilterContext) CreateDocFromControllers(params Type[] controllerTypes)
    {
        if (controllerTypes.Length == 0)
        {
            throw new ArgumentException("At least one controller type must be provided", nameof(controllerTypes));
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IWebHostEnvironment>());
        services.AddControllers()
        .ConfigureApplicationPartManager(manager =>
        {
            // Clear existing parts and feature providers
            manager.ApplicationParts.Clear();
            manager.FeatureProviders.Clear();

            // Add a custom feature provider that only includes the specific controller types
            manager.FeatureProviders.Add(new MultipleControllerFeatureProvider(controllerTypes));

            // Add assembly parts for all unique assemblies containing the controllers
            foreach (var assembly in controllerTypes.Select(t => t.Assembly).Distinct())
            {
                manager.ApplicationParts.Add(new AssemblyPart(assembly));
            }
        });
        services.AddSwaggerGen(config =>
        {
            config.SwaggerDoc("v1", new OpenApiInfo { Title = "Test API", Version = "v1" });
            config.CustomOperationIds(e => $"{e.ActionDescriptor.RouteValues["controller"]}_{e.ActionDescriptor.RouteValues["action"]}");
        });
        var serviceProvider = services.BuildServiceProvider();

        // Get API descriptions
        var allApiDescriptions = serviceProvider.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
                .ToList();

        if (allApiDescriptions.Count == 0)
        {
            throw new InvalidOperationException("No API descriptions found for controller, ensure your controllers are defined correctly (public, not nested, inherit from ControllerBase, etc.)");
        }

        // Generate the swagger document and context
        var document = serviceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var schemaGenerator = serviceProvider.GetRequiredService<ISchemaGenerator>();
        var context = new DocumentFilterContext(allApiDescriptions, schemaGenerator, new SchemaRepository());

        return (document, context);
    }
}

public class MultipleControllerFeatureProvider(params Type[] controllerTypes) : ControllerFeatureProvider
{
    private readonly HashSet<Type> _allowedControllerTypes = [.. controllerTypes];

    protected override bool IsController(TypeInfo typeInfo)
    {
        return _allowedControllerTypes.Contains(typeInfo.AsType())
          && typeInfo.IsClass
          && !typeInfo.IsAbstract
          && typeof(ControllerBase).IsAssignableFrom(typeInfo);
    }
}
