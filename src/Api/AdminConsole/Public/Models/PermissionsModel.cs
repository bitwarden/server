#nullable enable

using System.Text.Json.Serialization;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Public.Models;

/// <summary>
/// Represents a member's custom permissions if the member has a Custom role.
/// </summary>
public class PermissionsModel
{
    [JsonConstructor]
    public PermissionsModel() { }

    public PermissionsModel(Permissions? data)
    {
        if (data is null)
        {
            return;
        }

        AccessEventLogs = data.AccessEventLogs;
        AccessImportExport = data.AccessImportExport;
        AccessReports = data.AccessReports;
        CreateNewCollections = data.CreateNewCollections;
        EditAnyCollection = data.EditAnyCollection;
        DeleteAnyCollection = data.DeleteAnyCollection;
        ManageGroups = data.ManageGroups;
        ManagePolicies = data.ManagePolicies;
        ManageSso = data.ManageSso;
        ManageUsers = data.ManageUsers;
        ManageResetPassword = data.ManageResetPassword;
        ManageScim = data.ManageScim;
    }

    public bool AccessEventLogs { get; set; }
    public bool AccessImportExport { get; set; }
    public bool AccessReports { get; set; }
    public bool CreateNewCollections { get; set; }
    public bool EditAnyCollection { get; set; }
    public bool DeleteAnyCollection { get; set; }
    public bool ManageGroups { get; set; }
    public bool ManagePolicies { get; set; }
    public bool ManageSso { get; set; }
    public bool ManageUsers { get; set; }
    public bool ManageResetPassword { get; set; }
    public bool ManageScim { get; set; }

    public Permissions ToData()
    {
        return new Permissions
        {
            AccessEventLogs = AccessEventLogs,
            AccessImportExport = AccessImportExport,
            AccessReports = AccessReports,
            CreateNewCollections = CreateNewCollections,
            EditAnyCollection = EditAnyCollection,
            DeleteAnyCollection = DeleteAnyCollection,
            ManageGroups = ManageGroups,
            ManagePolicies = ManagePolicies,
            ManageSso = ManageSso,
            ManageUsers = ManageUsers,
            ManageResetPassword = ManageResetPassword,
            ManageScim = ManageScim,
        };
    }
}
