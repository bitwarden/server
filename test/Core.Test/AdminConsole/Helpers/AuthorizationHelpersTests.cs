using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Helpers;

public class AuthorizationHelpersTests
{
    [Fact]
    public void Permissions_Invert_InvertsAllPermissions()
    {
        var sut = new Permissions
        {
            AccessEventLogs = true,
            AccessReports = true,
            DeleteAnyCollection = true,
            ManagePolicies = true,
            ManageScim = true,
        };

        var result = sut.Invert();

        Assert.True(
            result
                is {
                    AccessEventLogs: false,
                    AccessImportExport: true,
                    AccessReports: false,
                    CreateNewCollections: true,
                    EditAnyCollection: true,
                    DeleteAnyCollection: false,
                    ManageGroups: true,
                    ManagePolicies: false,
                    ManageSso: true,
                    ManageUsers: true,
                    ManageResetPassword: true,
                    ManageScim: false
                }
        );
    }
}
