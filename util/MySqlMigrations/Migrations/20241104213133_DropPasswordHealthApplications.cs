using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class DropPasswordHealthApplications : Migration
{
    string dropPasswordHealtReportApplicationsTable = "2024-11-04-00_DropPasswordHealthReportApplications.sql";
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // migrationBuilder.CreateTable(
        //     name: "PasswordHealthReportApplications",
        //     columns: table => new
        //     {
        //         Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
        //         OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
        //         Uri = table.Column<string>(type: "longtext", nullable: true)
        //             .Annotation("MySql:CharSet", "utf8mb4"),
        //         CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
        //         RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        //     },
        //     constraints: table =>
        //     {
        //         table.PrimaryKey("PK_PasswordHealthReportApplications", x => x.Id);
        //         table.ForeignKey(
        //             name: "FK_PasswordHealthReportApplications_Organization_OrganizationId",
        //             column: x => x.OrganizationId,
        //             principalTable: "Organization",
        //             principalColumn: "Id",
        //             onDelete: ReferentialAction.Cascade);
        //     })
        //     .Annotation("MySql:CharSet", "utf8mb4");

        // migrationBuilder.CreateIndex(
        //     name: "IX_PasswordHealthReportApplications_OrganizationId",
        //     table: "PasswordHealthReportApplications",
        //     column: "OrganizationId");

        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(dropPasswordHealtReportApplicationsTable));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PasswordHealthReportApplications");
    }
}
