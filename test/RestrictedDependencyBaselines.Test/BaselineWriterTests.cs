using Bit.RestrictedDependencyBaselines.Baselines;
using Bit.RestrictedDependencyBaselines.Configuration;
using Bitwarden.Server.Sdk.RestrictedDependencies;
using Xunit;

namespace Bit.RestrictedDependencyBaselines.Test;

public class BaselineWriterTests
{
    [Fact]
    public void Write_NewBudget_WritesJsonFile()
    {
        var repoRoot = CreateTempDir();
        try
        {
            var budget = new BudgetModel(
                "Bit.Core.Services.IUserService",
                [],
                [new BudgetEntry(DependencyUsageType.Member, "M:Bit.Core.Services.IUserService.CanAccessPremium", "Billing", "M:Bit.Billing.Thing.Do", 2)]);

            BaselineWriter.Write(repoRoot, [budget], prune: false, TextWriter.Null);

            var expectedPath = Path.Combine(repoRoot, RepoLocator.BaselinesDirectory, "Bit.Core.Services.IUserService.json");
            Assert.True(File.Exists(expectedPath));
            Assert.Equal(budget.Serialize(), File.ReadAllText(expectedPath));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void Write_TypeNameEscapesBaselinesDirectory_Throws()
    {
        var repoRoot = CreateTempDir();
        try
        {
            var budget = new BudgetModel("../../evil", [], []);

            Assert.Throws<InvalidOperationException>(
                () => BaselineWriter.Write(repoRoot, [budget], prune: false, TextWriter.Null));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private static string CreateTempDir() => Path.Combine(Path.GetTempPath(), "bitwarden-test-" + Guid.NewGuid());
}
