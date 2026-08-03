using Bit.Core.Utilities;
using Xunit;

namespace Bit.Data.Test.Utilities;

public class CombGuidTests
{
    [Fact]
    public void Generate_Success()
    {
        // Arrange & Act
        var comb = CombGuid.Generate();

        // Assert
        Assert.NotEqual(Guid.Empty, comb);
        // TODO: Add more asserts to make sure important aspects of
        // the comb are working properly
    }

    public static IEnumerable<object[]> GenerateCombCases =>
    [
        [
            Guid.Parse("a58db474-43d8-42f1-b4ee-0c17647cd0c0"), // Input Guid
            new DateTime(2022, 3, 12, 12, 12, 0, DateTimeKind.Utc), // Input Time
            Guid.Parse("a58db474-43d8-42f1-b4ee-ae5600c90cc1"), // Expected Comb
        ],
        [
            Guid.Parse("f776e6ee-511f-4352-bb28-88513002bdeb"),
            new DateTime(2021, 5, 10, 10, 52, 0, DateTimeKind.Utc),
            Guid.Parse("f776e6ee-511f-4352-bb28-ad2400b313c1"),
        ],
        [
            Guid.Parse("51a25fc7-3cad-497d-8e2f-8d77011648a1"),
            new DateTime(1999, 2, 26, 16, 53, 13, DateTimeKind.Utc),
            Guid.Parse("51a25fc7-3cad-497d-8e2f-8d77011649cd"),
        ],
        [
            Guid.Parse("bfb8f353-3b32-4a9e-bef6-24fe0b54bfb0"),
            new DateTime(2024, 10, 20, 1, 32, 16, DateTimeKind.Utc),
            Guid.Parse("bfb8f353-3b32-4a9e-bef6-b20f00195780"),
        ],
    ];

    [Theory]
    [MemberData(nameof(GenerateCombCases))]
    public void Generate_WithInputs_Success(Guid inputGuid, DateTime inputTime, Guid expectedComb)
    {
        var comb = CombGuid.Generate(inputGuid, inputTime);

        Assert.Equal(expectedComb, comb);
    }
}
