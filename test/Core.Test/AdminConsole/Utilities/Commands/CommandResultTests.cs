using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities.Commands;

public class CommandResultTests
{
    public class TestItem
    {
        public Guid Id { get; set; }
        public string Value { get; set; }
    }

    public CommandResult<TestItem> BulkAction(IEnumerable<TestItem> items)
    {
        var itemList = items.ToList();
        var successfulItems = items.Where(x => x.Value == "SuccessfulRequest").ToArray();

        var failedItems = itemList.Except(successfulItems).ToArray();

        var notFound = failedItems.First(x => x.Value == "Failed due to not found");
        var invalidPermissions = failedItems.First(x => x.Value == "Failed due to invalid permissions");

        var notFoundError = new RecordNotFoundError<TestItem>(notFound);
        var insufficientPermissionsError = new InsufficientPermissionsError<TestItem>(invalidPermissions);

        return new Partial<TestItem>(successfulItems.ToArray(), [notFoundError, insufficientPermissionsError]);
    }

    [Theory]
    [BitAutoData]
    public void Partial_CommandResult_BulkRequestWithSuccessAndFailures(Guid successId1, Guid failureId1, Guid failureId2)
    {
        var listOfRecords = new List<TestItem>
        {
            new TestItem() { Id = successId1, Value = "SuccessfulRequest" },
            new TestItem() { Id = failureId1, Value = "Failed due to not found" },
            new TestItem() { Id = failureId2, Value = "Failed due to invalid permissions" }
        };

        var result = BulkAction(listOfRecords);

        Assert.IsType<Partial<TestItem>>(result);

        var failures = (result as Partial<TestItem>).Failures.ToArray();
        var success = (result as Partial<TestItem>).Successes.First();

        Assert.Equal(listOfRecords.First(), success);
        Assert.Equal(2, failures.Length);
    }
}
