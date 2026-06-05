using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Utilities;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Permissions))]
[JsonSerializable(typeof(MasterPasswordPolicyData))]
[JsonSerializable(typeof(SendOptionsPolicyData))]
[JsonSerializable(typeof(SendControlsPolicyData))]
[JsonSerializable(typeof(ResetPasswordDataModel))]
public partial class AdminConsoleJsonContext : JsonSerializerContext { }
