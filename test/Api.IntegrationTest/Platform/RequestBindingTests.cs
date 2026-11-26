using System.Reflection;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Bit.Api.IntegrationTest.Platform;

public class RequestBindingTests
{
    /// <summary>
    /// Entity parameters come from an action filter, with [BindNever], or from a binder that
    /// declares <see cref="BindingSource.Special"/>. Binding them from the request would let callers
    /// set any entity property, and the OpenAPI spec would list each property as a query parameter.
    /// ApiExplorer describes controller actions and minimal API endpoints alike.
    /// </summary>
    [Fact]
    public async Task EntityParameters_AreNotBoundFromTheRequest()
    {
        await using var factory = new ApiApplicationFactory();
        var descriptions = factory.GetService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items);

        var requestBoundEntities = (
            from description in descriptions
            from parameter in description.ParameterDescriptions
            where parameter.Source is { IsFromRequest: true }
                && (IsEntity(parameter.Type) || IsEntity(parameter.ModelMetadata?.ContainerType))
                && !IsBindNever(parameter.ParameterDescriptor)
            select $"{description.HttpMethod} {description.RelativePath} {parameter.Name}")
            .Distinct()
            .ToList();

        Assert.Empty(requestBoundEntities);
    }

    /// <summary>
    /// ApiExplorer still describes [BindNever] parameters, one description per entity property.
    /// </summary>
    private static bool IsBindNever(ParameterDescriptor? descriptor) =>
        descriptor is IParameterInfoParameterDescriptor { ParameterInfo: var info }
        && info.GetCustomAttribute<BindNeverAttribute>() != null;

    private static bool IsEntity(Type? type) =>
        type?.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableObject<>))
        ?? false;
}
