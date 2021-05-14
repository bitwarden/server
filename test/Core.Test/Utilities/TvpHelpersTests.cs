using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using AutoFixture.Xunit2;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Dapper;
using Newtonsoft.Json;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class TvpHelpersTests
    {
        [Fact]
        public void ToGuidIdArrayTVP_Success()
        {
            // Arrange
            var item0 = Guid.NewGuid();
            var item1 = Guid.NewGuid();

            var ids = new[] { item0, item1 };

            // Act
            var dt = ids.ToGuidIdArrayTVP();

            // Assert
            Assert.Single(dt.Columns);
            Assert.Equal("GuidId", dt.Columns[0].ColumnName);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal(item0, dt.Rows[0][0]);
            Assert.Equal(item1, dt.Rows[1][0]);
        }


        private DataTable BuildTvpManually(IEnumerable<ToTvpTestClass> objects)
        {
            var table = new DataTable();
            table.SetTypeName("[dbo].[ToTvpTestClass]");
            table.Columns.AddRange(new[]
            {
                new DataColumn("StringProp", typeof(string)),
                new DataColumn("IntProp", typeof(int)),
                new DataColumn("GuidProp", typeof(Guid)),
                new DataColumn("BoolProp", typeof(bool))
            });

            foreach (var obj in objects)
            {
                var row = table.NewRow();
                TvpHelpers.SetNullableField(row, 0, obj.StringProp);
                TvpHelpers.SetNullableField(row, 1, obj.IntProp);
                TvpHelpers.SetNullableField(row, 2, obj.GuidProp);
                TvpHelpers.SetNullableField(row, 3, obj.BoolProp);
                table.Rows.Add(row);
            }

            return table;
        }

        [Theory]
        [AutoData]
        public void ToTVP_Success(string tableTypeName, IEnumerable<ToTvpTestClass> tableData)
        {
            TvpHelpers._tvpConverterFactories[typeof(ToTvpTestClass)] = TvpHelpers.BuildTvpConverterFactory<ToTvpTestClass>();

            var actual = TvpHelpers.ToTVP(tableTypeName, tableData);
            var expectedDataTable = BuildTvpManually(tableData);
            Assert.Equal(tableTypeName, actual.GetTypeName());
            Assert.Equal(JsonConvert.SerializeObject(expectedDataTable), JsonConvert.SerializeObject(actual));
        }

        [Theory(Skip = "Performance test not necessary in CI environment")]
        [AutoData]
        public void ToTvp_PerformanceTest(IEnumerable<ToTvpTestClass> tableData)
        {
            var loop = 100000; // 100K
            TvpHelpers._tvpConverterFactories[typeof(ToTvpTestClass)] = TvpHelpers.BuildTvpConverterFactory<ToTvpTestClass>();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < loop; ++i)
            {
                TvpHelpers.ToTVP("[dbo].[ToTvpTestClass]", tableData);
            }
            Console.WriteLine($"ToTvp completed in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            for (int i = 0; i < loop; ++i)
            {
                BuildTvpManually(tableData);
            }
            Console.WriteLine($"BuildTvpManually completed in {sw.ElapsedMilliseconds} ms");
        }
    }

    public class ToTvpTestClass
    {
        [DbOrder(2)]
        public int? IntProp { get; set; }
        [DbOrder(1)]
        public string StringProp { get; set; }
        [DbOrder(3)]
        public Guid GuidProp { get; set; }
        [DbOrder(4)]
        public bool BoolProp { get; set; }
    }
}
