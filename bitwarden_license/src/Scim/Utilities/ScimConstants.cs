namespace Bit.Scim.Utilities;

public static class ScimConstants
{
    public const string Scim2SchemaListResponse = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    public const string Scim2SchemaError = "urn:ietf:params:scim:api:messages:2.0:Error";
    public const string Scim2SchemaUser = "urn:ietf:params:scim:schemas:core:2.0:User";
    public const string Scim2SchemaGroup = "urn:ietf:params:scim:schemas:core:2.0:Group";
}

public static class PatchOps
{
    public const string Replace = "replace";
    public const string Add = "add";
    public const string Remove = "remove";
}

public static class PatchPaths
{
    public const string Members = "members";
    public const string DisplayName = "displayname";
}
