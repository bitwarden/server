using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Checks for duplicate operation IDs in the Swagger document, and throws an error if any are found.
/// Operation IDs must be unique across the entire Swagger document according to the OpenAPI specification,
/// but we use controller action names to generate them, which can lead to duplicates if a Controller function
/// has multiple HTTP methods or if a Controller has overloaded functions.
/// </summary>
public class CheckDuplicateOperationIdsDocumentFilter(bool printDuplicates = true) : IDocumentFilter
{
    public bool PrintDuplicates { get; } = printDuplicates;

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var operationIdMap = new Dictionary<string, List<(string Path, OpenApiPathItem PathItem, OperationType Method, OpenApiOperation Operation)>>();

        foreach (var (path, pathItem) in swaggerDoc.Paths)
        {
            foreach (var operation in pathItem.Operations)
            {
                if (!operationIdMap.TryGetValue(operation.Value.OperationId, out var list))
                {
                    list = [];
                    operationIdMap[operation.Value.OperationId] = list;
                }

                list.Add((path, pathItem, operation.Key, operation.Value));

            }
        }

        // Find duplicates
        var duplicates = operationIdMap.Where((kvp) => kvp.Value.Count > 1).ToList();
        if (duplicates.Count > 0)
        {
            if (PrintDuplicates)
            {
                Console.WriteLine($"\n######## Duplicate operationIds found in the schema ({duplicates.Count} found) ########\n");

                Console.WriteLine("## Common causes of duplicate operation IDs:");
                Console.WriteLine("- Multiple HTTP methods (GET, POST, etc.) on the same controller function");
                Console.WriteLine("    Solution: Split the methods into separate functions, and if appropiate, mark the deprecated ones with [Obsolete]");
                Console.WriteLine();
                Console.WriteLine("- Overloaded controller functions with the same name");
                Console.WriteLine("    Solution: Rename the overloaded functions to have unique names, or combine them into a single function with optional parameters");
                Console.WriteLine();

                Console.WriteLine("## The duplicate operation IDs are:");

                foreach (var (operationId, duplicate) in duplicates)
                {
                    Console.WriteLine($"- operationId: {operationId}");
                    foreach (var (path, pathItem, method, operation) in duplicate)
                    {
                        Console.Write($"    {method.ToString().ToUpper()} {path}");


                        if (operation.Extensions.TryGetValue("x-source-file", out var sourceFile) && operation.Extensions.TryGetValue("x-source-line", out var sourceLine))
                        {
                            var sourceFileString = ((Microsoft.OpenApi.Any.OpenApiString)sourceFile).Value;
                            var sourceLineString = ((Microsoft.OpenApi.Any.OpenApiInteger)sourceLine).Value;

                            Console.WriteLine($"    {sourceFileString}:{sourceLineString}");
                        }
                        else
                        {
                            Console.WriteLine();
                        }
                    }
                    Console.WriteLine("\n");
                }
            }

            throw new InvalidOperationException($"Duplicate operation IDs found in Swagger schema");
        }
    }
}
