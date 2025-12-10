using Bit.Core.Auth.Models.Api.Request.Accounts;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Api.Request.Accounts;

/// <summary>
/// Snapshot tests to ensure the string constants in <see cref="MarketingInitiativeConstants"/> do not change unintentionally.
/// If you intentionally change any of these values, please update the tests to reflect the new expected values.
/// </summary>
public class MarketingInitiativeConstantsSnapshotTests
{
    [Fact]
    public void MarketingInitiativeConstants_HaveCorrectValues()
    {
        // Assert
        Assert.Equal("premium", MarketingInitiativeConstants.Premium);
    }
}
