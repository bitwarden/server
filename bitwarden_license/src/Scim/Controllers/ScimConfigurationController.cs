using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Scim.Controllers;

/// <summary>
/// SCIM 2.0 discovery endpoints per RFC 7643 §5/§7 and RFC 7644 §4.
/// These are not org-scoped and do not require authentication.
/// </summary>
[AllowAnonymous]
[Produces("application/scim+json")]
public class ScimConfigurationController : Controller
{
    /// <summary>
    /// GET /v2/ServiceProviderConfig — RFC 7643 §5
    /// Advertises supported SCIM features.
    /// </summary>
    [HttpGet("~/v2/serviceproviderconfig")]
    public IActionResult GetServiceProviderConfig()
    {
        var config = new
        {
            schemas = new[] { ScimConstants.Scim2SchemaServiceProviderConfig },
            patch = new { supported = true },
            bulk = new
            {
                supported = false,
                maxOperations = 0,
                maxPayloadSize = 0
            },
            filter = new
            {
                supported = true,
                maxResults = 200
            },
            changePassword = new { supported = false },
            sort = new { supported = false },
            etag = new { supported = false },
            authenticationSchemes = new[]
            {
                new
                {
                    type = "oauthbearertoken",
                    name = "OAuth Bearer Token",
                    description = "Authentication scheme using the OAuth Bearer Token standard",
                    specUri = "https://tools.ietf.org/html/rfc6750",
                    primary = true
                }
            },
            meta = new { resourceType = "ServiceProviderConfig" }
        };

        return Ok(config);
    }

    /// <summary>
    /// GET /v2/Schemas — RFC 7643 §7
    /// Returns schema definitions for User and Group resources.
    /// </summary>
    [HttpGet("~/v2/schemas")]
    public IActionResult GetSchemas()
    {
        var schemas = new[]
        {
            GetUserSchema(),
            GetGroupSchema()
        };

        var response = new
        {
            schemas = new[] { ScimConstants.Scim2SchemaListResponse },
            totalResults = schemas.Length,
            startIndex = 1,
            itemsPerPage = schemas.Length,
            Resources = schemas
        };

        return Ok(response);
    }

    /// <summary>
    /// GET /v2/Schemas/{id} — RFC 7643 §7
    /// Returns a single schema by its URN identifier.
    /// </summary>
    [HttpGet("~/v2/schemas/{id}")]
    public IActionResult GetSchema(string id)
    {
        var decodedId = Uri.UnescapeDataString(id);

        return decodedId switch
        {
            ScimConstants.Scim2SchemaUser => Ok(GetUserSchema()),
            ScimConstants.Scim2SchemaGroup => Ok(GetGroupSchema()),
            _ => NotFound(new
            {
                schemas = new[] { ScimConstants.Scim2SchemaError },
                detail = $"Schema '{decodedId}' not found.",
                status = 404
            })
        };
    }

    /// <summary>
    /// GET /v2/ResourceTypes — RFC 7644 §4
    /// Returns metadata about User and Group resource types.
    /// </summary>
    [HttpGet("~/v2/resourcetypes")]
    public IActionResult GetResourceTypes()
    {
        var resourceTypes = new[]
        {
            new
            {
                schemas = new[] { ScimConstants.Scim2SchemaResourceType },
                id = "User",
                name = "User",
                endpoint = "/Users",
                description = "User resource",
                schema = ScimConstants.Scim2SchemaUser,
                schemaExtensions = Array.Empty<object>(),
                meta = new { resourceType = "ResourceType" }
            },
            new
            {
                schemas = new[] { ScimConstants.Scim2SchemaResourceType },
                id = "Group",
                name = "Group",
                endpoint = "/Groups",
                description = "Group resource",
                schema = ScimConstants.Scim2SchemaGroup,
                schemaExtensions = Array.Empty<object>(),
                meta = new { resourceType = "ResourceType" }
            }
        };

        var response = new
        {
            schemas = new[] { ScimConstants.Scim2SchemaListResponse },
            totalResults = resourceTypes.Length,
            startIndex = 1,
            itemsPerPage = resourceTypes.Length,
            Resources = resourceTypes
        };

        return Ok(response);
    }

    private static object GetUserSchema()
    {
        return new
        {
            schemas = new[] { ScimConstants.Scim2SchemaSchema },
            id = ScimConstants.Scim2SchemaUser,
            name = "User",
            description = "User Account",
            attributes = new object[]
            {
                new
                {
                    name = "userName",
                    type = "string",
                    multiValued = false,
                    description = "Unique identifier for the User, typically the email address",
                    required = true,
                    caseExact = false,
                    mutability = "readWrite",
                    returned = "default",
                    uniqueness = "server"
                },
                new
                {
                    name = "displayName",
                    type = "string",
                    multiValued = false,
                    description = "The name of the User, suitable for display",
                    required = false,
                    caseExact = false,
                    mutability = "readWrite",
                    returned = "default",
                    uniqueness = "none"
                },
                new
                {
                    name = "name",
                    type = "complex",
                    multiValued = false,
                    description = "The components of the user's real name",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        new
                        {
                            name = "formatted",
                            type = "string",
                            multiValued = false,
                            description = "The full name, including all middle names, titles, and suffixes",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        },
                        new
                        {
                            name = "givenName",
                            type = "string",
                            multiValued = false,
                            description = "The given name of the User",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        },
                        new
                        {
                            name = "middleName",
                            type = "string",
                            multiValued = false,
                            description = "The middle name(s) of the User",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        },
                        new
                        {
                            name = "familyName",
                            type = "string",
                            multiValued = false,
                            description = "The family name of the User",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        }
                    }
                },
                new
                {
                    name = "emails",
                    type = "complex",
                    multiValued = true,
                    description = "Email addresses for the User",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        new
                        {
                            name = "value",
                            type = "string",
                            multiValued = false,
                            description = "Email address value",
                            required = true,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        },
                        new
                        {
                            name = "type",
                            type = "string",
                            multiValued = false,
                            description = "The type of email (e.g., work, home)",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        },
                        new
                        {
                            name = "primary",
                            type = "boolean",
                            multiValued = false,
                            description = "Indicates if this is the primary email",
                            required = false,
                            mutability = "readWrite",
                            returned = "default"
                        }
                    }
                },
                new
                {
                    name = "active",
                    type = "boolean",
                    multiValued = false,
                    description = "A Boolean value indicating the User's administrative status",
                    required = false,
                    mutability = "readWrite",
                    returned = "default"
                },
                new
                {
                    name = "externalId",
                    type = "string",
                    multiValued = false,
                    description = "An identifier for the resource as defined by the provisioning client",
                    required = false,
                    caseExact = false,
                    mutability = "readWrite",
                    returned = "default",
                    uniqueness = "none"
                },
                new
                {
                    name = "id",
                    type = "string",
                    multiValued = false,
                    description = "Unique identifier for the SCIM resource as defined by the service provider",
                    required = true,
                    caseExact = true,
                    mutability = "readOnly",
                    returned = "always",
                    uniqueness = "server"
                }
            },
            meta = new
            {
                resourceType = "Schema",
                location = "/v2/Schemas/" + ScimConstants.Scim2SchemaUser
            }
        };
    }

    private static object GetGroupSchema()
    {
        return new
        {
            schemas = new[] { ScimConstants.Scim2SchemaSchema },
            id = ScimConstants.Scim2SchemaGroup,
            name = "Group",
            description = "Group",
            attributes = new object[]
            {
                new
                {
                    name = "displayName",
                    type = "string",
                    multiValued = false,
                    description = "A human-readable name for the Group",
                    required = true,
                    caseExact = false,
                    mutability = "readWrite",
                    returned = "default",
                    uniqueness = "none"
                },
                new
                {
                    name = "members",
                    type = "complex",
                    multiValued = true,
                    description = "A list of members of the Group",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        new
                        {
                            name = "value",
                            type = "string",
                            multiValued = false,
                            description = "Identifier of the member of this Group",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default"
                        },
                        new
                        {
                            name = "$ref",
                            type = "reference",
                            multiValued = false,
                            description = "The URI corresponding to a SCIM resource that is a member of this Group",
                            required = false,
                            caseExact = false,
                            mutability = "readWrite",
                            returned = "default",
                            referenceTypes = new[] { "User" }
                        }
                    }
                },
                new
                {
                    name = "externalId",
                    type = "string",
                    multiValued = false,
                    description = "An identifier for the resource as defined by the provisioning client",
                    required = false,
                    caseExact = false,
                    mutability = "readWrite",
                    returned = "default",
                    uniqueness = "none"
                },
                new
                {
                    name = "id",
                    type = "string",
                    multiValued = false,
                    description = "Unique identifier for the SCIM resource as defined by the service provider",
                    required = true,
                    caseExact = true,
                    mutability = "readOnly",
                    returned = "always",
                    uniqueness = "server"
                }
            },
            meta = new
            {
                resourceType = "Schema",
                location = "/v2/Schemas/" + ScimConstants.Scim2SchemaGroup
            }
        };
    }
}
