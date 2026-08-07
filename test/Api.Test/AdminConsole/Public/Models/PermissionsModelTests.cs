using Bit.Api.AdminConsole.Public.Models;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Models;

public class PermissionsModelTests
{
    /// <summary>
    /// <see cref="PermissionsModel"/> is a hand-rolled mirror of <see cref="Permissions"/>, and
    /// <see cref="PermissionsModel.ToData"/> rebuilds the whole object. A permission missing from the mirror is
    /// therefore silently cleared whenever a member is updated through the public API, so every permission must
    /// survive a round trip.
    /// </summary>
    [Fact]
    public void ToData_RoundTripsEveryPermission()
    {
        var permissionProperties = typeof(Permissions)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool));

        foreach (var permission in permissionProperties)
        {
            var data = new Permissions();
            permission.SetValue(data, true);

            var roundTripped = new PermissionsModel(data).ToData();

            Assert.True((bool)permission.GetValue(roundTripped)!,
                $"{permission.Name} was lost when round-tripping through {nameof(PermissionsModel)}. " +
                $"Add it to the constructor, its own property, and {nameof(PermissionsModel.ToData)}.");
        }
    }
}
