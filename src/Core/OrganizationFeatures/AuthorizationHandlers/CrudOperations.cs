using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;


public class CrudOperationRequirement : OperationAuthorizationRequirement { }

public static class CrudOperations
{
    public static readonly CrudOperationRequirement Create = new() { Name = "Create" };
    public static readonly CrudOperationRequirement Read = new() { Name = "Read" };
    public static readonly CrudOperationRequirement Update = new() { Name = "Update" };
    public static readonly CrudOperationRequirement Delete = new() { Name = "Delete" };
}

