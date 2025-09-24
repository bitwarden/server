#nullable enable

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Adds source file and line number information to the Swagger operation description.
/// This can be useful for locating the source code of the operation in the repository,
/// as the generated names are based on the HTTP path, and are hard to search for.
/// </summary>
public class SourceFileLineOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {

        var (fileName, lineNumber) = GetSourceFileLine(context.MethodInfo);
        if (fileName != null && lineNumber > 0)
        {
            // Also add the information as extensions, so other tools can use it in the future
            operation.Extensions.Add("x-source-file", new OpenApiString(fileName));
            operation.Extensions.Add("x-source-line", new OpenApiInteger(lineNumber));
        }
    }

    private static (string? fileName, int lineNumber) GetSourceFileLine(MethodInfo methodInfo)
    {
        // Get the location of the PDB file associated with the module of the method
        var pdbPath = Path.ChangeExtension(methodInfo.Module.FullyQualifiedName, ".pdb");
        if (!File.Exists(pdbPath)) return (null, 0);

        // Open the PDB file and read the metadata
        using var pdbStream = File.OpenRead(pdbPath);
        using var metadataReaderProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var metadataReader = metadataReaderProvider.GetMetadataReader();

        // If the method is async, the compiler will generate a state machine,
        // so we can't look for the original method, but we instead need to look
        // for the MoveNext method of the state machine.
        var attr = methodInfo.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attr?.StateMachineType != null)
        {
            var moveNext = attr.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (moveNext != null) methodInfo = moveNext;
        }

        // Once we have the  method, we can get its sequence points
        var handle = (MethodDefinitionHandle)MetadataTokens.Handle(methodInfo.MetadataToken);
        if (handle.IsNil) return (null, 0);
        var sequencePoints = metadataReader.GetMethodDebugInformation(handle).GetSequencePoints();

        // Iterate through the sequence points and pick the first one that has a valid line number
        foreach (var sp in sequencePoints)
        {
            var docName = metadataReader.GetDocument(sp.Document).Name;
            if (sp.StartLine != 0 && sp.StartLine != SequencePoint.HiddenLine && !docName.IsNil)
            {
                var fileName = metadataReader.GetString(docName);
                var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
                var relativeFileName = repoRoot != null ? Path.GetRelativePath(repoRoot, fileName) : fileName;
                return (relativeFileName, sp.StartLine);
            }
        }

        return (null, 0);
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        return dir?.FullName;
    }
}
