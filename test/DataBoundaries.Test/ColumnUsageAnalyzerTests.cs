using System.Collections.Immutable;
using Bit.DataBoundaries.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Bit.DataBoundaries.Test;

public class ColumnUsageAnalyzerTests
{
    private const string RepoRoot = "/repo";
    private const string EntityMetadataName = "Bit.Core.Organization";
    private const string EntityPath = "src/Core/Organization.cs";
    private const string ConsumerPath = "src/Core/Consumer.cs";

    private const string SeatsEntity = """
        namespace Bit.Core;

        public class Organization
        {
            public int Seats { get; set; }
        }
        """;

    /// <summary>
    /// The analyzer needs a compilation that resolves <c>object</c>, <c>System.Linq</c> and the
    /// rest of the framework, or every fixture fails to compile and yields no operations at all.
    /// The test host already has that closure on disk, so it is reused rather than rebuilt.
    /// </summary>
    private static readonly Lazy<MetadataReference[]> _platformReferences = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray());

    [Fact]
    public async Task Analyze_ReportsAQualifiedPropertyReadAsADirectRead()
    {
        var accesses = await AnalyzeAsync(SeatsEntity, """
            namespace Bit.Core;

            public class Consumer
            {
                public int Read(Organization organization) => organization.Seats;
            }
            """);

        var access = Assert.Single(accesses);

        Assert.Equal("Seats", access.Column);
        Assert.Equal(AccessKind.Read, access.Kind);
        Assert.Equal(ConsumerPath, access.File);
        Assert.Null(access.ViaHelper);
    }

    [Fact]
    public async Task Analyze_ReportsAnAssignmentAsAWrite()
    {
        var accesses = await AnalyzeAsync(SeatsEntity, ConsumerRunning("organization.Seats = 5;"));

        Assert.Equal(AccessKind.Write, Assert.Single(accesses).Kind);
    }

    [Fact]
    public async Task Analyze_ReportsAnObjectInitializerMemberAsAWrite()
    {
        var accesses = await AnalyzeAsync(SeatsEntity, """
            namespace Bit.Core;

            public class Consumer
            {
                public Organization Create() => new Organization { Seats = 9 };
            }
            """);

        Assert.Equal(AccessKind.Write, Assert.Single(accesses).Kind);
    }

    [Theory]
    [InlineData("organization.Seats += 1;")]
    [InlineData("organization.Seats++;")]
    public async Task Analyze_ReportsAReadModifyWriteAsReadWrite(string statement)
    {
        var accesses = await AnalyzeAsync(SeatsEntity, ConsumerRunning(statement));

        Assert.Equal(AccessKind.ReadWrite, Assert.Single(accesses).Kind);
    }

    [Fact]
    public async Task Analyze_ReportsARefArgumentAsReadWrite()
    {
        var accesses = await AnalyzeAsync("""
            namespace Bit.Core;

            public class Organization
            {
                private int _seats;

                public ref int Seats => ref _seats;
            }
            """, """
            namespace Bit.Core;

            public class Consumer
            {
                public void Act(Organization organization) => Take(ref organization.Seats);

                private static void Take(ref int seats) => seats = 1;
            }
            """);

        Assert.Equal(AccessKind.ReadWrite, Assert.Single(accesses).Kind);
    }

    [Fact]
    public async Task Analyze_IgnoresAPropertyNamedInsideNameof()
    {
        var accesses = await AnalyzeAsync(SeatsEntity, """
            namespace Bit.Core;

            public class Consumer
            {
                public string Name(Organization organization) => nameof(organization.Seats);
            }
            """);

        Assert.Empty(accesses);
    }

    [Fact]
    public async Task Analyze_ReportsAPropertyPatternAsARead()
    {
        var accesses = await AnalyzeAsync(SeatsEntity, """
            namespace Bit.Core;

            public class Consumer
            {
                public bool HasSeats(Organization organization) => organization is { Seats: > 0 };
            }
            """);

        Assert.Equal(AccessKind.Read, Assert.Single(accesses).Kind);
    }

    [Fact]
    public async Task Analyze_AttributesEveryColumnAHelperReadsToTheCaller()
    {
        var accesses = await AnalyzeAsync("""
            namespace Bit.Core;

            public class Organization
            {
                public short? MaxStorageGb { get; set; }

                public long? Storage { get; set; }

                public long StorageBytesRemaining()
                {
                    return ((MaxStorageGb ?? 0) * 1073741824L) - (Storage ?? 0);
                }
            }
            """, """
            namespace Bit.Core;

            public class Consumer
            {
                public long Remaining(Organization organization) => organization.StorageBytesRemaining();
            }
            """);

        var consumerAccesses = accesses.Where(a => a.File == ConsumerPath).ToArray();

        Assert.Equal(["MaxStorageGb", "Storage"], consumerAccesses.Select(a => a.Column));
        Assert.All(consumerAccesses, a => Assert.Equal("StorageBytesRemaining", a.ViaHelper));
    }

    [Fact]
    public async Task Analyze_AttributesAColumnReachedThroughTwoHelperHopsToTheCaller()
    {
        var accesses = await AnalyzeAsync("""
            namespace Bit.Core;

            public class Organization
            {
                public int Seats { get; set; }

                public int Outer() => Inner();

                public int Inner() => Seats;
            }
            """, """
            namespace Bit.Core;

            public class Consumer
            {
                public int Ask(Organization organization) => organization.Outer();
            }
            """);

        var access = Assert.Single(accesses.Where(a => a.File == ConsumerPath));

        Assert.Equal("Seats", access.Column);
        Assert.Equal("Outer", access.ViaHelper);
    }

    [Fact]
    public async Task Analyze_AttributesAColumnAHelperReadsOnlyInsideALambdaToTheCaller()
    {
        var accesses = await AnalyzeAsync("""
            using System.Linq;

            namespace Bit.Core;

            public class Organization
            {
                public bool Use2fa { get; set; }

                public bool TwoFactorIsEnabled()
                {
                    var providers = new[] { 1, 2 };
                    return providers.Any(provider => provider > 0 && Use2fa);
                }
            }
            """, """
            namespace Bit.Core;

            public class Consumer
            {
                public bool Enabled(Organization organization) => organization.TwoFactorIsEnabled();
            }
            """);

        var access = Assert.Single(accesses.Where(a => a.File == ConsumerPath));

        Assert.Equal("Use2fa", access.Column);
        Assert.Equal("TwoFactorIsEnabled", access.ViaHelper);
    }

    [Fact]
    public async Task Analyze_IgnoresTheSamePropertyDeclaredByAnotherImplementerOfTheInterface()
    {
        var accesses = await AnalyzeAsync(
            new Fixture(EntityPath, """
                namespace Bit.Core;

                public interface IStorable
                {
                    short? MaxStorageGb { get; set; }
                }

                public class Organization : IStorable
                {
                    public short? MaxStorageGb { get; set; }
                }

                public class User : IStorable
                {
                    public short? MaxStorageGb { get; set; }
                }
                """),
            new Fixture("src/Core/OrganizationConsumer.cs", """
                namespace Bit.Core;

                public class OrganizationConsumer
                {
                    public short? Read(Organization organization) => organization.MaxStorageGb;
                }
                """),
            new Fixture("src/Core/UserConsumer.cs", """
                namespace Bit.Core;

                public class UserConsumer
                {
                    public short? Read(User user) => user.MaxStorageGb;
                }
                """));

        Assert.Equal("src/Core/OrganizationConsumer.cs", Assert.Single(accesses).File);
    }

    [Fact]
    public async Task Analyze_SkipsDocumentsOutsideTheProductSourceTree()
    {
        var accesses = await AnalyzeAsync(
            new Fixture(EntityPath, SeatsEntity),
            new Fixture("test/Core.Test/ConsumerTests.cs", """
                namespace Bit.Core;

                public class ConsumerTests
                {
                    public int Read(Organization organization) => organization.Seats;
                }
                """));

        Assert.Empty(accesses);
    }

    [Fact]
    public async Task Analyze_SkipsGeneratedMigrations()
    {
        var accesses = await AnalyzeAsync(
            new Fixture(EntityPath, SeatsEntity),
            new Fixture("src/Infrastructure.EntityFramework/Migrations/AddSeats.cs", """
                namespace Bit.Core;

                public class AddSeats
                {
                    public int Read(Organization organization) => organization.Seats;
                }
                """));

        Assert.Empty(accesses);
    }

    [Fact]
    public async Task Analyze_DoesNotAttributeAHelperToACallFromInsideTheEntity()
    {
        var accesses = await AnalyzeAsync(
            new Fixture(EntityPath, """
                namespace Bit.Core;

                public class Organization
                {
                    public int Seats { get; set; }

                    public int Outer() => Inner();

                    public int Inner() => Seats;
                }
                """));

        // The entity's own read of Seats is still reported, because the entity itself lives under
        // src/. What must not appear is a helper attribution for Outer() calling Inner().
        Assert.All(accesses, a => Assert.Null(a.ViaHelper));
    }

    [Fact]
    public async Task Analyze_CollapsesTwoIdenticalHelperAttributionsOnOneLine()
    {
        var accesses = await AnalyzeAsync("""
            namespace Bit.Core;

            public class Organization
            {
                public int Seats { get; set; }

                public int Total() => Seats;
            }
            """, """
            namespace Bit.Core;

            public class Consumer
            {
                public int Sum(Organization organization) => organization.Total() + organization.Total();
            }
            """);

        Assert.Single(accesses.Where(a => a.File == ConsumerPath));
    }

    private sealed record Fixture(string RelativePath, string Source);

    private static string ConsumerRunning(string statement) => $$"""
        namespace Bit.Core;

        public class Consumer
        {
            public void Act(Organization organization)
            {
                {{statement}}
            }
        }
        """;

    private static Task<ImmutableArray<ColumnAccess>> AnalyzeAsync(string entitySource, string consumerSource) =>
        AnalyzeAsync(new Fixture(EntityPath, entitySource), new Fixture(ConsumerPath, consumerSource));

    private static async Task<ImmutableArray<ColumnAccess>> AnalyzeAsync(params Fixture[] fixtures)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        // SolutionLoader.AnalyzableProjects and the analyzer both filter on project name and path,
        // so the fixture project has to stand in for src/Core specifically.
        workspace.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            name: "Core",
            assemblyName: "Core",
            LanguageNames.CSharp,
            filePath: $"{RepoRoot}/src/Core/Core.csproj",
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: _platformReferences.Value));

        foreach (var fixture in fixtures)
        {
            workspace.AddDocument(DocumentInfo.Create(
                DocumentId.CreateNewId(projectId),
                Path.GetFileName(fixture.RelativePath),
                filePath: $"{RepoRoot}/{fixture.RelativePath}",
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(fixture.Source),
                    VersionStamp.Create()))));
        }

        var solution = workspace.CurrentSolution;
        var compilation = await solution.GetProject(projectId)!.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);

        var errors = compilation.GetDiagnostics(cancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToArray();
        if (errors.Length > 0)
        {
            Assert.Fail($"Fixture did not compile:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        var entity = compilation.GetTypeByMetadataName(EntityMetadataName);
        Assert.NotNull(entity);

        return await ColumnUsageAnalyzer.AnalyzeAsync(solution, entity, RepoRoot, cancellationToken);
    }
}
