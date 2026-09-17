using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Bit.DataBoundaries.Roslyn;

/// <summary>
/// Attributes column reads and writes by symbol.
///
/// This deliberately does not use <c>SymbolFinder.FindReferencesAsync</c>. That API cascades to
/// interface members, and both <c>Organization</c> and <c>User</c> implement
/// <c>IStorable.MaxStorageGb</c>, so a search for one table's property returns the other table's
/// call sites too. Measured on this repository: 102 of 178 results belonged to the wrong table.
/// Comparing <c>IPropertyReferenceOperation.Property.ContainingType</c> against the entity symbol
/// keeps the two apart, which is the entire reason for doing this semantically instead of by text.
/// </summary>
internal static class ColumnUsageAnalyzer
{
    /// <summary>
    /// Projects that map every table by design, so a reference from them says nothing about one
    /// boundary depending on another's data.
    /// </summary>
    private static readonly string[] _infrastructureProjects =
    [
        "Infrastructure.Dapper",
        "Infrastructure.EntityFramework",
        "Commercial.Infrastructure.EntityFramework",
    ];

    private sealed record RawAccess(
        string Column,
        string File,
        int Line,
        AccessKind Kind,
        string? EnclosingEntityMethod);

    private sealed record EntityCall(
        string Method,
        string File,
        int Line,
        string? EnclosingEntityMethod);

    public static async Task<ImmutableArray<ColumnAccess>> AnalyzeAsync(
        Solution solution,
        INamedTypeSymbol entity,
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var rawAccesses = new List<RawAccess>();
        var entityCalls = new List<EntityCall>();

        foreach (var project in SolutionLoader.AnalyzableProjects(solution))
        {
            if (_infrastructureProjects.Contains(project.Name))
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            var localEntity = ResolveInCompilation(compilation, entity);
            if (localEntity is null)
            {
                continue;
            }

            foreach (var document in project.Documents)
            {
                if (document.FilePath is null || !InConsumerScope(document.FilePath, repoRoot))
                {
                    continue;
                }

                var model = await document.GetSemanticModelAsync(cancellationToken);
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                if (model is null || root is null)
                {
                    continue;
                }

                var relative = Relative(document.FilePath, repoRoot);

                foreach (var expression in root.DescendantNodes().OfType<ExpressionSyntax>())
                {
                    var operation = model.GetOperation(expression, cancellationToken);
                    if (operation is null || IsInsideNameOf(expression))
                    {
                        continue;
                    }

                    var line = expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                    switch (operation)
                    {
                        case IPropertyReferenceOperation reference
                            when IsOn(reference.Property.ContainingType, localEntity):
                            rawAccesses.Add(new RawAccess(
                                reference.Property.Name,
                                relative,
                                line,
                                Classify(reference),
                                EnclosingEntityMethod(model, expression, localEntity, cancellationToken)));
                            break;

                        // A call to a method the entity itself declares can read columns without
                        // naming them. StorageBytesRemaining() is the worked example: it reads
                        // Storage and MaxStorageGb, and a caller mentions neither.
                        case IInvocationOperation invocation
                            when IsOn(invocation.TargetMethod.ContainingType, localEntity):
                            entityCalls.Add(new EntityCall(
                                invocation.TargetMethod.Name,
                                relative,
                                line,
                                EnclosingEntityMethod(model, expression, localEntity, cancellationToken)));
                            break;
                    }
                }
            }
        }

        var direct = rawAccesses
            .Select(a => new ColumnAccess(a.Column, a.File, a.Line, a.Kind, null))
            .ToList();

        var indirect = ResolveHelperReads(rawAccesses, entityCalls);

        // Kind is a sort key, not decoration: a helper that both reads and writes one column emits
        // two accesses at the same file and line, and their order would otherwise fall through to
        // hash-set enumeration order, which varies between processes.
        return
        [
            .. direct.Concat(indirect)
                .Distinct()
                .OrderBy(a => a.Column, StringComparer.Ordinal)
                .ThenBy(a => a.File, StringComparer.Ordinal)
                .ThenBy(a => a.Line)
                .ThenBy(a => a.ViaHelper, StringComparer.Ordinal)
                .ThenBy(a => a.Kind)
        ];
    }

    /// <summary>
    /// Attributes the columns an entity method touches to the places that call it.
    ///
    /// Without this, a column read only through a helper looks unused. The doc's worked example is
    /// <c>StorageBytesRemaining()</c>, whose callers name neither <c>Storage</c> nor
    /// <c>MaxStorageGb</c>. The closure iterates because entity methods call each other, so a
    /// column reached through several hops still lands on the caller; the emitted access records
    /// only the helper the caller named, not the hops behind it.
    /// </summary>
    private static List<ColumnAccess> ResolveHelperReads(
        List<RawAccess> rawAccesses,
        List<EntityCall> entityCalls)
    {
        // Columns each entity method touches directly.
        var methodColumns = rawAccesses
            .Where(a => a.EnclosingEntityMethod is not null)
            .GroupBy(a => a.EnclosingEntityMethod!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => (a.Column, a.Kind)).ToHashSet(),
                StringComparer.Ordinal);

        // Entity method to entity method, for the transitive step.
        var methodCalls = entityCalls
            .Where(c => c.EnclosingEntityMethod is not null)
            .GroupBy(c => c.EnclosingEntityMethod!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.Method).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        // Fixed point. Bounded by method count, and a self-call cannot add anything new.
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var (method, callees) in methodCalls)
            {
                if (!methodColumns.TryGetValue(method, out var own))
                {
                    own = [];
                    methodColumns[method] = own;
                }

                foreach (var callee in callees.Where(c => !string.Equals(c, method, StringComparison.Ordinal)))
                {
                    if (methodColumns.TryGetValue(callee, out var calleeColumns))
                    {
                        var before = own.Count;
                        own.UnionWith(calleeColumns);
                        changed |= own.Count != before;
                    }
                }
            }
        }

        var results = new List<ColumnAccess>();
        foreach (var call in entityCalls)
        {
            // A call from inside the entity is part of the closure, not a consumer of it.
            if (call.EnclosingEntityMethod is not null)
            {
                continue;
            }

            if (!methodColumns.TryGetValue(call.Method, out var columns))
            {
                continue;
            }

            foreach (var (column, kind) in columns)
            {
                results.Add(new ColumnAccess(column, call.File, call.Line, kind, call.Method));
            }
        }

        return results;
    }

    /// <summary>
    /// The entity method containing this expression, or null when the expression is not inside one.
    /// This is what separates the entity's own internal reads from a consumer's.
    /// </summary>
    private static string? EnclosingEntityMethod(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol entity,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetEnclosingSymbol(node.SpanStart, cancellationToken);

        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            // A this-capturing lambda or local function declared inside an entity method is itself
            // a method symbol on the entity, but an unnamed one. Returning it would key the access
            // under a name no caller can invoke, orphaning it from the closure.
            if (current is IMethodSymbol { MethodKind: not (MethodKind.AnonymousFunction or MethodKind.LocalFunction) } method
                && IsOn(method.ContainingType, entity))
            {
                // A property accessor reports as get_X; name it after the property instead.
                return method.AssociatedSymbol?.Name ?? method.Name;
            }
        }

        return null;
    }

    private static INamedTypeSymbol? ResolveInCompilation(Compilation compilation, INamedTypeSymbol entity)
    {
        if (compilation.Assembly.Name == entity.ContainingAssembly.Name)
        {
            return entity;
        }

        var metadataName = entity
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);

        return compilation.GetTypeByMetadataName(metadataName);
    }

    private static bool IsOn(INamedTypeSymbol? containingType, INamedTypeSymbol entity) =>
        containingType is not null
        && SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, entity.OriginalDefinition);

    private static AccessKind Classify(IPropertyReferenceOperation reference)
    {
        for (IOperation? current = reference; current?.Parent is not null; current = current.Parent)
        {
            switch (current.Parent)
            {
                // Covers both `org.Seats = n` and `new Organization { Seats = n }`; the object
                // initializer form is a simple assignment whose target is the property.
                case ISimpleAssignmentOperation assignment when ReferenceEquals(assignment.Target, current):
                    return AccessKind.Write;

                // `org.Seats += 1` and `org.Seats++` both read the old value and write a new one.
                case ICompoundAssignmentOperation compound when ReferenceEquals(compound.Target, current):
                case IIncrementOrDecrementOperation increment when ReferenceEquals(increment.Target, current):
                    return AccessKind.ReadWrite;

                // A `ref` or `out` argument can be written by the callee.
                case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out }:
                    return AccessKind.ReadWrite;

                // Anything else that merely contains the reference does not change its kind.
                default:
                    if (current.Parent is not IConversionOperation and not IParenthesizedOperation)
                    {
                        return AccessKind.Read;
                    }

                    break;
            }
        }

        return AccessKind.Read;
    }

    /// <summary>
    /// <c>nameof(org.Seats)</c> yields a compile-time string. It is neither a read nor a write, and
    /// counting it would attribute a consumer that never touches the data.
    /// </summary>
    private static bool IsInsideNameOf(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } })
            {
                return true;
            }
        }

        return false;
    }

    private static bool InConsumerScope(string filePath, string repoRoot)
    {
        var relative = Relative(filePath, repoRoot);

        if (!relative.StartsWith("src/", StringComparison.Ordinal)
            && !relative.StartsWith("bitwarden_license/src/", StringComparison.Ordinal))
        {
            return false;
        }

        // Generated EF migrations restate every column and would swamp the real signal.
        return !relative.Contains("/Migrations/", StringComparison.Ordinal);
    }

    private static string Relative(string filePath, string repoRoot) =>
        Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');
}
