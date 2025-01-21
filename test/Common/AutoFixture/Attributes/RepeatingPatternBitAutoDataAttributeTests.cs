#nullable enable
using Xunit;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class RepeatingPatternBitAutoDataAttributeTests
{
    public class OneParam1 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public OneParam1(TestDataContext context)
        {
            context.SetData(1, [], [], []);
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([])]
        public void NoPattern_NoTestExecution(string autoDataFilled)
        {
            Assert.NotEmpty(autoDataFilled);
            _context.TestExecuted();
        }
    }

    public class OneParam2 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public OneParam2(TestDataContext context)
        {
            context.SetData(2, [false, true], [], []);
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([false, true])]
        public void TrueFalsePattern_2Executions(bool first, string autoDataFilled)
        {
            Assert.True(_context.ExpectedBooleans1.Remove(first));
            Assert.NotEmpty(autoDataFilled);
            _context.TestExecuted();
        }
    }

    public class OneParam3 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public OneParam3(TestDataContext context)
        {
            context.SetData(4, [], [], [null, "", " ", "\t"]);
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([null, "", " ", "\t"])]
        public void NullableEmptyStringPattern_4Executions(string? first, string autoDataFilled)
        {
            Assert.True(_context.ExpectedStrings.Remove(first));
            Assert.NotEmpty(autoDataFilled);
            _context.TestExecuted();
        }
    }

    public class OneParam4 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public OneParam4(TestDataContext context)
        {
            context.SetData(6, [], [], [null, "", " ", "\t", "\n", " \t\n"]);
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([null, "", " ", "\t"])] // 4 executions
        [BitAutoData("\n")] // 1 execution
        [BitAutoData(" \t\n", "test data")] // 1 execution
        public void MixedPatternsWithBitAutoData_6Executions(string? first, string autoDataFilled)
        {
            Assert.True(_context.ExpectedStrings.Remove(first));
            Assert.NotEmpty(autoDataFilled);
            if (first == " \t\n")
            {
                Assert.Equal("test data", autoDataFilled);
            }

            _context.TestExecuted();
        }
    }

    public class TwoParams1 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public TwoParams1(TestDataContext context)
        {
            context.SetData(8, TestDataContext.GenerateData([false, true], 4), [],
                TestDataContext.GenerateData([null, "", " ", "\t"], 2));
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([false, true], [null, "", " ", "\t"])]
        public void TrueFalsePatternFirstNullableEmptyStringPatternSecond_8Executions(
            bool first, string? second,
            string autoDataFilled)
        {
            Assert.True(_context.ExpectedBooleans1.Remove(first));
            Assert.True(_context.ExpectedStrings.Remove(second));
            Assert.NotEmpty(autoDataFilled);
            _context.TestExecuted();
        }
    }

    public class TwoParams2 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public TwoParams2(TestDataContext context)
        {
            context.SetData(8, TestDataContext.GenerateData([false, true], 4), [],
                TestDataContext.GenerateData([null, "", " ", "\t"], 2));
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([null, "", " ", "\t"], [false, true])]
        public void NullableEmptyStringPatternFirstTrueFalsePatternSecond_8Executions(
            string? first, bool second,
            string autoDataFilled)
        {
            Assert.True(_context.ExpectedStrings.Remove(first));
            Assert.True(_context.ExpectedBooleans1.Remove(second));
            Assert.NotEmpty(autoDataFilled);
            _context.TestExecuted();
        }
    }

    public class TwoParams3 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public TwoParams3(TestDataContext context)
        {
            var expectedBooleans1 = TestDataContext.GenerateData([false], 4);
            expectedBooleans1.AddRange(TestDataContext.GenerateData([true], 5));
            var expectedStrings = TestDataContext.GenerateData([null, "", " "], 2);
            expectedStrings.AddRange(["\t", "\n", " \t\n"]);
            context.SetData(9, expectedBooleans1, [], expectedStrings);
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([null, "", " "], [false, true])] // 6 executions
        [RepeatingPatternBitAutoData(["\t"], [false])] // 1 execution
        [BitAutoData("\n", true)] // 1 execution
        [BitAutoData(" \t\n", true, "test data")] // 1 execution
        public void MixedPatternsWithBitAutoData_9Executions(
            string? first, bool second,
            string autoDataFilled)
        {
            Assert.True(_context.ExpectedStrings.Remove(first));
            Assert.True(_context.ExpectedBooleans1.Remove(second));
            Assert.NotEmpty(autoDataFilled);
            if (first == " \t\n")
            {
                Assert.Equal("test data", autoDataFilled);
            }

            _context.TestExecuted();
        }
    }

    public class ThreeParams1 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public ThreeParams1(TestDataContext context)
        {
            context.SetData(16, TestDataContext.GenerateData([false, true], 8),
                TestDataContext.GenerateData([false, true], 8),
                TestDataContext.GenerateData([null, "", " ", "\t"], 4));
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([false, true], [null, "", " ", "\t"], [false, true])]
        public void TrueFalsePatternFirstNullableEmptyStringPatternSecondFalsePatternThird_16Executions(
            bool first, string? second, bool third,
            string autoDataFilled)
        {
            Assert.True(_context.ExpectedBooleans1.Remove(first));
            Assert.True(_context.ExpectedStrings.Remove(second));
            Assert.True(_context.ExpectedBooleans2.Remove(third));
            Assert.NotEmpty(autoDataFilled);
            _context.TestExecuted();
        }
    }

    public class ThreeParams2 : IClassFixture<TestDataContext>
    {
        private readonly TestDataContext _context;

        public ThreeParams2(TestDataContext context)
        {
            var expectedBooleans1 = TestDataContext.GenerateData([false, true], 6);
            expectedBooleans1.AddRange(TestDataContext.GenerateData([true], 3));
            var expectedBooleans2 = TestDataContext.GenerateData([false, true], 7);
            expectedBooleans2.Add(true);
            var expectedStrings = TestDataContext.GenerateData([null, "", " "], 4);
            expectedStrings.AddRange(["\t", "\t", " \t\n"]);
            context.SetData(15, expectedBooleans1, expectedBooleans2, expectedStrings);
            _context = context;
        }

        [Theory]
        [RepeatingPatternBitAutoData([false, true], [null, "", " "], [false, true])] // 12 executions
        [RepeatingPatternBitAutoData([true], ["\t"], [false, true])] // 2 executions
        [BitAutoData(true, " \t\n", true, "test data")] // 1 execution
        public void MixedPatternsWithBitAutoData_15Executions(
            bool first, string? second, bool third,
            string autoDataFilled)
        {
            Assert.True(_context.ExpectedBooleans1.Remove(first));
            Assert.True(_context.ExpectedStrings.Remove(second));
            Assert.True(_context.ExpectedBooleans2.Remove(third));
            Assert.NotEmpty(autoDataFilled);
            if (second == " \t\n")
            {
                Assert.Equal("test data", autoDataFilled);
            }

            _context.TestExecuted();
        }
    }
}

public class TestDataContext : IDisposable
{
    internal List<bool> ExpectedBooleans1 = [];
    internal List<bool> ExpectedBooleans2 = [];

    internal List<string?> ExpectedStrings = [];

    private int _expectedExecutionCount;
    private bool _dataSet;

    public void TestExecuted()
    {
        _expectedExecutionCount--;
    }

    public void SetData(int expectedExecutionCount, List<bool> expectedBooleans1, List<bool> expectedBooleans2,
        List<string?> expectedStrings)
    {
        if (_dataSet)
        {
            return;
        }

        _expectedExecutionCount = expectedExecutionCount;
        ExpectedBooleans1 = expectedBooleans1;
        ExpectedBooleans2 = expectedBooleans2;
        ExpectedStrings = expectedStrings;

        _dataSet = true;
    }

    public static List<T> GenerateData<T>(List<T> list, int count)
    {
        var repeatedList = new List<T>();
        for (var i = 0; i < count; i++)
        {
            repeatedList.AddRange(list);
        }

        return repeatedList;
    }

    public void Dispose()
    {
        Assert.Equal(0, _expectedExecutionCount);
        Assert.Empty(ExpectedBooleans1);
        Assert.Empty(ExpectedBooleans2);
        Assert.Empty(ExpectedStrings);
    }
}
