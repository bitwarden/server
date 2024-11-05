using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class UpdatePasswordHealthReportApplication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // migrationBuilder.DropForeignKey(
        //     name: "FK_PasswordHealthReportApplications_Organization_OrganizationId",
        //     table: "PasswordHealthReportApplications");

        // migrationBuilder.DropPrimaryKey(
        //     name: "PK_PasswordHealthReportApplications",
        //     table: "PasswordHealthReportApplications");

        // migrationBuilder.RenameTable(
        //     name: "PasswordHealthReportApplications",
        //     newName: "PasswordHealthReportApplication");

        // migrationBuilder.RenameIndex(
        //     name: "IX_PasswordHealthReportApplications_OrganizationId",
        //     table: "PasswordHealthReportApplication",
        //     newName: "IX_PasswordHealthReportApplication_OrganizationId");

        // migrationBuilder.AddPrimaryKey(
        //     name: "PK_PasswordHealthReportApplication",
        //     table: "PasswordHealthReportApplication",
        //     column: "Id");

        // migrationBuilder.CreateIndex(
        //     name: "IX_PasswordHealthReportApplication_Id",
        //     table: "PasswordHealthReportApplication",
        //     column: "Id");

        // migrationBuilder.AddForeignKey(
        //     name: "FK_PasswordHealthReportApplication_Organization_OrganizationId",
        //     table: "PasswordHealthReportApplication",
        //     column: "OrganizationId",
        //     principalTable: "Organization",
        //     principalColumn: "Id",
        //     onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // migrationBuilder.DropForeignKey(
        //     name: "FK_PasswordHealthReportApplication_Organization_OrganizationId",
        //     table: "PasswordHealthReportApplication");

        // migrationBuilder.DropPrimaryKey(
        //     name: "PK_PasswordHealthReportApplication",
        //     table: "PasswordHealthReportApplication");

        // migrationBuilder.DropIndex(
        //     name: "IX_PasswordHealthReportApplication_Id",
        //     table: "PasswordHealthReportApplication");

        // migrationBuilder.RenameTable(
        //     name: "PasswordHealthReportApplication",
        //     newName: "PasswordHealthReportApplications");

        // migrationBuilder.RenameIndex(
        //     name: "IX_PasswordHealthReportApplication_OrganizationId",
        //     table: "PasswordHealthReportApplications",
        //     newName: "IX_PasswordHealthReportApplications_OrganizationId");

        // migrationBuilder.AddPrimaryKey(
        //     name: "PK_PasswordHealthReportApplications",
        //     table: "PasswordHealthReportApplications",
        //     column: "Id");

        // migrationBuilder.AddForeignKey(
        //     name: "FK_PasswordHealthReportApplications_Organization_OrganizationId",
        //     table: "PasswordHealthReportApplications",
        //     column: "OrganizationId",
        //     principalTable: "Organization",
        //     principalColumn: "Id",
        //     onDelete: ReferentialAction.Cascade);
    }
}
