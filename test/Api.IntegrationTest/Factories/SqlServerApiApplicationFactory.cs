using Bit.IntegrationTestCommon;

#nullable enable

namespace Bit.Api.IntegrationTest.Factories;

public class SqlServerApiApplicationFactory() : ApiApplicationFactory(new SqlServerTestDatabase());
