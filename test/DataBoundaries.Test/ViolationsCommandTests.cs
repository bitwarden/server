using Bit.DataBoundaries.Commands;

namespace Bit.DataBoundaries.Test;

public class ViolationsCommandTests
{
    [Fact]
    public void Violations_ReportsAToolFailure_UntilTheRoslynStageExists()
    {
        // Consumer data cannot be derived from schema and CODEOWNERS alone, so the verb returns no
        // verdict rather than a partial one. Delete this test when the Roslyn stage can answer.
        Assert.Equal(ExitCode.ToolFailure, new ViolationsCommand().Execute());
    }
}
