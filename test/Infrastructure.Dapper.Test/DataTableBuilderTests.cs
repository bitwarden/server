using System.Data;

namespace Bit.Infrastructure.Dapper.Test;

public class DataTableBuilderTests
{
    public class TestItem
    {
        // Normal value type
        public int Id { get; set; }

        // Normal reference type
        public string? Name { get; set; }

        // Nullable value type
        public DateTime? DeletedDate { get; set; }
        public object? ObjectProp { get; set; }
        public DefaultEnum DefaultEnum { get; set; }
        public DefaultEnum? NullableDefaultEnum { get; set; }
        public ByteEnum ByteEnum { get; set; }
        public ByteEnum? NullableByteEnum { get; set; }

        public int Method()
        {
            throw new NotImplementedException();
        }
    }

    public enum DefaultEnum
    {
        Zero,
        One,
    }

    public enum ByteEnum : byte
    {
        Zero,
        One,
    }

    [Fact]
    public void DataTableBuilder_Works()
    {
        var dtb = new DataTableBuilder<TestItem>(
            [
                i => i.Id,
                i => i.Name,
                i => i.DeletedDate,
                i => i.ObjectProp,
                i => i.DefaultEnum,
                i => i.NullableDefaultEnum,
                i => i.ByteEnum,
                i => i.NullableByteEnum,
            ]
        );

        var table = dtb.Build(
            [
                new TestItem
                {
                    Id = 4,
                    Name = "Test",
                    DeletedDate = new DateTime(2024, 8, 8),
                    ObjectProp = 1,
                    DefaultEnum = DefaultEnum.One,
                    NullableDefaultEnum = DefaultEnum.Zero,
                    ByteEnum = ByteEnum.One,
                    NullableByteEnum = ByteEnum.Zero,
                },
                new TestItem
                {
                    Id = int.MaxValue,
                    Name = null,
                    DeletedDate = null,
                    ObjectProp = "Hi",
                    DefaultEnum = DefaultEnum.Zero,
                    NullableDefaultEnum = null,
                    ByteEnum = ByteEnum.Zero,
                    NullableByteEnum = null,
                },
            ]
        );

        Assert.Collection(
            table.Columns.Cast<DataColumn>(),
            column =>
            {
                Assert.Equal("Id", column.ColumnName);
                Assert.Equal(typeof(int), column.DataType);
            },
            column =>
            {
                Assert.Equal("Name", column.ColumnName);
                Assert.Equal(typeof(string), column.DataType);
            },
            column =>
            {
                Assert.Equal("DeletedDate", column.ColumnName);
                // Checking that it will unwrap the `Nullable<T>`
                Assert.Equal(typeof(DateTime), column.DataType);
            },
            column =>
            {
                Assert.Equal("ObjectProp", column.ColumnName);
                Assert.Equal(typeof(object), column.DataType);
            },
            column =>
            {
                Assert.Equal("DefaultEnum", column.ColumnName);
                Assert.Equal(typeof(int), column.DataType);
            },
            column =>
            {
                Assert.Equal("NullableDefaultEnum", column.ColumnName);
                Assert.Equal(typeof(int), column.DataType);
            },
            column =>
            {
                Assert.Equal("ByteEnum", column.ColumnName);
                Assert.Equal(typeof(byte), column.DataType);
            },
            column =>
            {
                Assert.Equal("NullableByteEnum", column.ColumnName);
                Assert.Equal(typeof(byte), column.DataType);
            }
        );

        Assert.Collection(
            table.Rows.Cast<DataRow>(),
            row =>
            {
                Assert.Collection(
                    row.ItemArray,
                    item => Assert.Equal(4, item),
                    item => Assert.Equal("Test", item),
                    item => Assert.Equal(new DateTime(2024, 8, 8), item),
                    item => Assert.Equal(1, item),
                    item => Assert.Equal((int)DefaultEnum.One, item),
                    item => Assert.Equal((int)DefaultEnum.Zero, item),
                    item => Assert.Equal((byte)ByteEnum.One, item),
                    item => Assert.Equal((byte)ByteEnum.Zero, item)
                );
            },
            row =>
            {
                Assert.Collection(
                    row.ItemArray,
                    item => Assert.Equal(int.MaxValue, item),
                    item => Assert.Equal(DBNull.Value, item),
                    item => Assert.Equal(DBNull.Value, item),
                    item => Assert.Equal("Hi", item),
                    item => Assert.Equal((int)DefaultEnum.Zero, item),
                    item => Assert.Equal(DBNull.Value, item),
                    item => Assert.Equal((byte)ByteEnum.Zero, item),
                    item => Assert.Equal(DBNull.Value, item)
                );
            }
        );
    }

    [Fact]
    public void DataTableBuilder_ThrowsOnInvalidExpression()
    {
        var argException = Assert.Throws<ArgumentException>(
            () => new DataTableBuilder<TestItem>([i => i.Method()])
        );
        Assert.Equal(
            "Could not determine the property info from the given expression 'i => Convert(i.Method(), Object)'.",
            argException.Message
        );
    }

    [Fact]
    public void DataTableBuilder_ThrowsOnRepeatExpression()
    {
        var argException = Assert.Throws<ArgumentException>(
            () => new DataTableBuilder<TestItem>([i => i.Id, i => i.Id])
        );
        Assert.Equal(
            "Property with name 'Id' was already added, properties can only be added once.",
            argException.Message
        );
    }
}
