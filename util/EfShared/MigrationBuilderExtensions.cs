using System.Runtime.CompilerServices;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.EfShared;

// This file is a manual addition to a project that it helps, a project that chooses to compile it
// should have a project reference to Core.csproj and a package reference to Microsoft.EntityFrameworkCore.Design
// The reason for this is that if it belonged to it's own library you would have to add manual references to the above
// and manage the version for the EntityFrameworkCore package. This way it also doesn't create another dll
// To include this you can view examples in the MySqlMigrations and PostgresMigrations .csproj files. 
// <Compile Include="..\EfShared\MigrationBuilderExtensions.cs" />

public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Reads an embedded resource for it's SQL contents and formats it with the specified direction for easier custom migration steps
    /// </summary>
    /// <param name="migrationBuilder">The MigrationBuilder instance the sql should be applied to</param>
    /// <param name="resourceName">The file name portion of the resource name, it is assumed to be in a Scripts folder</param>
    /// <param name="dir">The direction of the migration taking place</param>
    public static void SqlResource(this MigrationBuilder migrationBuilder, string resourceName, [CallerMemberName] string dir = null)
    {
        var formattedResourceName = string.IsNullOrEmpty(dir) ? resourceName : string.Format(resourceName, dir);

        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(
            $"Scripts.{formattedResourceName}"));
    }
}
