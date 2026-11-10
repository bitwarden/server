using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Utilities;

public sealed class ScimAttributeProjection
{
    private readonly string[] _paths;
    private readonly bool _include;
    private readonly JsonSerializerOptions _options;

    public ScimAttributeProjection(HttpRequest request)
    {
        var attributes = request.Query.ContainsKey("attributes");
        var excluded = request.Query.ContainsKey("excludedAttributes");
        if (attributes && excluded)
        {
            throw new BadRequestException("Specify either attributes or excludedAttributes, not both.");
        }

        _include = attributes;
        IsActive = attributes || excluded;
        _options = request.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        if (!IsActive)
        {
            _paths = [];
            return;
        }

        _paths = request.Query[attributes ? "attributes" : "excludedAttributes"].ToString()
            .Split(',').Select(path => path.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (_paths.Any(path => !IsValidPath(path)))
        {
            throw new BadRequestException("Attribute selection must contain comma-separated attribute paths.");
        }

        // Core schema-qualified names address the same attributes as their unqualified form.
        _paths = _paths.Select(path => path.StartsWith(ScimConstants.Scim2SchemaUser + ":", StringComparison.OrdinalIgnoreCase)
            ? path[(ScimConstants.Scim2SchemaUser.Length + 1)..]
            : path.StartsWith(ScimConstants.Scim2SchemaGroup + ":", StringComparison.OrdinalIgnoreCase)
                ? path[(ScimConstants.Scim2SchemaGroup.Length + 1)..] : path).ToArray();
    }

    public bool IsActive { get; }

    private static bool IsValidPath(string path)
    {
        var separator = path.LastIndexOf(':');
        if (separator >= 0)
        {
            if (!path.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) ||
                path[..separator].Any(character => !char.IsAsciiLetterOrDigit(character) &&
                    character is not ':' and not '.' and not '-' and not '_'))
            {
                return false;
            }
            path = path[(separator + 1)..];
        }
        return path.Split('.').All(part => part.Equals("$ref", StringComparison.OrdinalIgnoreCase) ||
            (part.Length > 0 && char.IsAsciiLetter(part[0]) &&
                part.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')));
    }

    public bool Includes(string path)
    {
        if (!IsActive)
        {
            return true;
        }

        return _include
            ? _paths.Any(selected => selected.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                selected.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(selected + ".", StringComparison.OrdinalIgnoreCase))
            : !_paths.Any(excluded => excluded.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(excluded + ".", StringComparison.OrdinalIgnoreCase));
    }

    public object Apply(object response, bool list = false)
    {
        if (!IsActive)
        {
            return response;
        }

        var node = JsonSerializer.SerializeToNode(response, response.GetType(), _options)
            ?? throw new InvalidOperationException("Unable to serialize SCIM resource.");
        if (list)
        {
            foreach (var resource in node["resources"]!.AsArray())
            {
                Filter(resource!, "");
            }
        }
        else
        {
            Filter(node, "");
        }
        return node;
    }

    private void Filter(JsonNode node, string prefix)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    Filter(item, prefix);
                }
            }
        }
        else if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (prefix.Length == 0 && property.Key is "id" or "schemas")
                {
                    continue;
                }
                var path = prefix.Length == 0 ? property.Key : prefix + "." + property.Key;
                if (!Includes(path))
                {
                    obj.Remove(property.Key);
                }
                else if (property.Value != null)
                {
                    Filter(property.Value, path);
                }
            }
        }
    }
}
