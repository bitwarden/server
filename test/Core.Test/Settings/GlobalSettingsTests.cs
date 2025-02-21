using Bit.Core.Settings;
using Xunit;

namespace Bit.Core.Test.Settings;

public class GlobalSettingsTests
{
    public class SqlSettingsTests
    {
        private const string _testingConnectionString =
            "Server=server;Database=database;User Id=user;Password=password;";

        private const string _testingReadOnlyConnectionString =
            "Server=server_read;Database=database_read;User Id=user_read;Password=password_read;";

        [Fact]
        public void ConnectionString_ValueInDoubleQuotes_Stripped()
        {
            var settings = new GlobalSettings.SqlSettings { ConnectionString = $"\"{_testingConnectionString}\"", };

            Assert.Equal(_testingConnectionString, settings.ConnectionString);
        }

        [Fact]
        public void ConnectionString_ValueWithoutDoubleQuotes_TheSameValue()
        {
            var settings = new GlobalSettings.SqlSettings { ConnectionString = _testingConnectionString };

            Assert.Equal(_testingConnectionString, settings.ConnectionString);
        }

        [Fact]
        public void ConnectionString_SetTwice_ReturnsSecondConnectionString()
        {
            var settings = new GlobalSettings.SqlSettings { ConnectionString = _testingConnectionString };

            Assert.Equal(_testingConnectionString, settings.ConnectionString);

            var newConnectionString = $"{_testingConnectionString}_new";
            settings.ConnectionString = newConnectionString;

            Assert.Equal(newConnectionString, settings.ConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_ValueInDoubleQuotes_Stripped()
        {
            var settings = new GlobalSettings.SqlSettings
            {
                ReadOnlyConnectionString = $"\"{_testingReadOnlyConnectionString}\"",
            };

            Assert.Equal(_testingReadOnlyConnectionString, settings.ReadOnlyConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_ValueWithoutDoubleQuotes_TheSameValue()
        {
            var settings = new GlobalSettings.SqlSettings
            {
                ReadOnlyConnectionString = _testingReadOnlyConnectionString
            };

            Assert.Equal(_testingReadOnlyConnectionString, settings.ReadOnlyConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_NotSet_DefaultsToConnectionString()
        {
            var settings = new GlobalSettings.SqlSettings { ConnectionString = _testingConnectionString };

            Assert.Equal(_testingConnectionString, settings.ReadOnlyConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_Set_ReturnsReadOnlyConnectionString()
        {
            var settings = new GlobalSettings.SqlSettings
            {
                ConnectionString = _testingConnectionString,
                ReadOnlyConnectionString = _testingReadOnlyConnectionString
            };

            Assert.Equal(_testingReadOnlyConnectionString, settings.ReadOnlyConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_SetTwice_ReturnsSecondReadOnlyConnectionString()
        {
            var settings = new GlobalSettings.SqlSettings
            {
                ConnectionString = _testingConnectionString,
                ReadOnlyConnectionString = _testingReadOnlyConnectionString
            };

            Assert.Equal(_testingReadOnlyConnectionString, settings.ReadOnlyConnectionString);

            var newReadOnlyConnectionString = $"{_testingReadOnlyConnectionString}_new";
            settings.ReadOnlyConnectionString = newReadOnlyConnectionString;

            Assert.Equal(newReadOnlyConnectionString, settings.ReadOnlyConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_NotSetAndConnectionStringSetTwice_ReturnsSecondConnectionString()
        {
            var settings = new GlobalSettings.SqlSettings { ConnectionString = _testingConnectionString };

            Assert.Equal(_testingConnectionString, settings.ReadOnlyConnectionString);

            var newConnectionString = $"{_testingConnectionString}_new";
            settings.ConnectionString = newConnectionString;

            Assert.Equal(newConnectionString, settings.ReadOnlyConnectionString);
        }

        [Fact]
        public void ReadOnlyConnectionString_SetAndConnectionStringSetTwice_ReturnsReadOnlyConnectionString()
        {
            var settings = new GlobalSettings.SqlSettings
            {
                ConnectionString = _testingConnectionString,
                ReadOnlyConnectionString = _testingReadOnlyConnectionString
            };

            Assert.Equal(_testingReadOnlyConnectionString, settings.ReadOnlyConnectionString);

            var newConnectionString = $"{_testingConnectionString}_new";
            settings.ConnectionString = newConnectionString;

            Assert.Equal(_testingReadOnlyConnectionString, settings.ReadOnlyConnectionString);
        }
    }
}
